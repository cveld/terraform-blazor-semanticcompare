import { readFile } from 'node:fs/promises';
import test from 'node:test';
import assert from 'node:assert/strict';

const source = await readFile(new URL('../BlazorSemanticCompare/wwwroot/js/webmcp.js', import.meta.url), 'utf8');
async function setup(context) {
    Object.defineProperty(globalThis, 'navigator', { value: { modelContext: context }, configurable: true });
    return import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}#${Math.random()}`);
}
function registry() {
    const tools = new Map();
    return {
        tools,
        registerTool(tool) { if (tools.has(tool.name)) throw new Error('Duplicate'); tools.set(tool.name, tool); },
        unregisterTool(name) { tools.delete(name); }
    };
}
const json = response => JSON.parse(response.content[0].text);

test('unsupported browsers gracefully decline registration', async () => {
    const api = await setup(undefined);
    assert.equal(api.registerTools({}), false);
    api.unregisterTools();
});

test('all tools register and unregister, including re-registration', async () => {
    const context = registry();
    const api = await setup(context);
    assert.equal(api.registerTools({}), true);
    assert.deepEqual([...context.tools.keys()], ['load-terraform-plan', 'get-plan-status', 'open-semantic-compare']);
    const old = context.tools.get('get-plan-status');
    assert.equal(api.registerTools({}), true);
    assert.equal((await old.execute({})).isError, true);
    api.unregisterTools();
    assert.equal(context.tools.size, 0);
});

test('large unicode plan is chunked and successfully completed', async () => {
    const context = registry();
    const api = await setup(context);
    let transferred = '';
    const calls = [];
    api.registerTools({ async invokeMethodAsync(name, value) {
        calls.push(name);
        if (name === 'AppendWebMcpPlan') {
            assert.ok(value.length <= 2000);
            assert.ok(!(value.charCodeAt(value.length - 1) >= 0xD800 && value.charCodeAt(value.length - 1) <= 0xDBFF));
            transferred += value;
        }
        if (name === 'CompleteWebMcpPlan') return { success: true, resourceChanges: 1 };
    } });
    const plan = JSON.stringify({ format_version: '1.2', resource_changes: [], padding: '😀\\"'.repeat(12000) });
    const response = await context.tools.get('load-terraform-plan').execute({ plan });
    assert.equal(response.isError, false);
    assert.equal(transferred, plan);
    assert.equal(json(response).resourceChanges, 1);
    assert.equal(calls.at(-1), 'CancelWebMcpPlan');
});

test('invalid, empty and oversized imports fail without interop', async () => {
    const context = registry();
    const api = await setup(context);
    api.registerTools({ invokeMethodAsync() { assert.fail('No interop expected'); } });
    for (const plan of ['', 'no json', '[]', 'null', 42, 'a'.repeat(20_000_001), '😀'.repeat(5_000_001)]) {
        assert.equal((await context.tools.get('load-terraform-plan').execute({ plan })).isError, true);
    }
});

test('server validation errors and disconnects are tool errors', async () => {
    const context = registry();
    const api = await setup(context);
    api.registerTools({ async invokeMethodAsync(name) {
        if (name === 'CompleteWebMcpPlan') return { success: false, error: 'Invalid resource_changes' };
    } });
    assert.equal((await context.tools.get('load-terraform-plan').execute({ plan: '{}' })).isError, true);
    api.registerTools({ async invokeMethodAsync() { throw new Error('Disconnected'); } });
    const response = await context.tools.get('load-terraform-plan').execute({ plan: '{}' });
    assert.equal(response.isError, true);
    assert.match(json(response).error, /Disconnected/);
});

test('status and comparison delegate only to the current viewer', async () => {
    const context = registry();
    const api = await setup(context);
    const calls = [];
    api.registerTools({ async invokeMethodAsync(...args) { calls.push(args); return { success: true }; } });
    assert.equal((await context.tools.get('get-plan-status').execute({})).isError, false);
    assert.equal((await context.tools.get('open-semantic-compare').execute({ address: 'demo.example', property: 'rules' })).isError, false);
    assert.deepEqual(calls, [['GetWebMcpPlanStatus'], ['OpenWebMcpSemanticCompare', 'demo.example', 'rules']]);
    assert.equal((await context.tools.get('open-semantic-compare').execute({})).isError, true);
});

test('partial registration failure removes only owned tools', async () => {
    const context = registry();
    context.tools.set('get-plan-status', { external: true });
    const api = await setup(context);
    const warning = console.warn;
    try { console.warn = () => {}; assert.equal(api.registerTools({}), false); }
    finally { console.warn = warning; }
    assert.deepEqual([...context.tools.keys()], ['get-plan-status']);
});

test('concurrent imports are rejected while first upload completes', async () => {
    const context = registry();
    const api = await setup(context);
    let release;
    const gate = new Promise(resolve => { release = resolve; });
    api.registerTools({ async invokeMethodAsync(name) {
        if (name === 'BeginWebMcpPlan') await gate;
        if (name === 'CompleteWebMcpPlan') return { success: true };
    } });
    const load = context.tools.get('load-terraform-plan');
    const first = load.execute({ plan: '{}' });
    assert.equal((await load.execute({ plan: '{}' })).isError, true);
    release();
    assert.equal((await first).isError, false);
});
