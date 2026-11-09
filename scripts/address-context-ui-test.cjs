// Run with PLAYWRIGHT_CORE_PATH pointing at an installed playwright-core package.
const { chromium } = require(process.env.PLAYWRIGHT_CORE_PATH || 'playwright-core');
const assert = require('node:assert/strict');

(async () => {
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage();
        await page.goto('http://localhost:5206');
        await page.locator('#agent-code').waitFor();
        await page.locator('#terraform-plan-upload').setInputFiles(process.argv[2]);
        await page.getByTestId('plan-loaded').waitFor();
        const resource = page.locator('li.list-group-item').filter({ hasText: 'Allow-management-to-workload' });
        await resource.locator('button').first().click();
        const context = resource.getByTestId('firewall-address-context');
        await context.waitFor();
        assert.match(await context.innerText(), /ipg-sql/);
        assert.match(await context.innerText(), /10\.200\.24\.0\/24/);
        assert.match(await context.innerText(), /ipg-vms/);
        await resource.locator('tr').filter({ hasText: 'network_rule_collection' }).getByRole('button', { name: 'Semantic Compare' }).click();
        await page.getByText('Differences:', { exact: true }).waitFor();
        assert.equal(await page.getByText('Kon geen geldige JSON vergelijken.', { exact: true }).count(), 0);
        assert.equal(await page.getByTestId('firewall-address-context').count(), 2);
        console.log('UI checks passed: planned SQL/VM ranges visible; computed diff renders.');
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
