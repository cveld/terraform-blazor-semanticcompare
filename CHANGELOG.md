# Changelog

## [0.4.0](https://github.com/cveld/terraform-blazor-semanticcompare/compare/v0.3.0...v0.4.0) (2026-11-09)


### Features

* expand WebMCP tools with chunked and validated plan imports ([2824679](https://github.com/cveld/terraform-blazor-semanticcompare/commit/2824679782094b1966d25fea766be14d7bb2d55e))
* show IP-group address ranges alongside firewall rule changes ([e5c0bc5](https://github.com/cveld/terraform-blazor-semanticcompare/commit/e5c0bc5fade21746851f9770603c0bee7a1407df))
* update page title when a Terraform plan is loaded ([d4a96f6](https://github.com/cveld/terraform-blazor-semanticcompare/commit/d4a96f6620a164310a7ec1b2548c830d2b358cc9))


### Bug Fixes

* abort watcher startup when process discovery fails ([a0f05d4](https://github.com/cveld/terraform-blazor-semanticcompare/commit/a0f05d4403235cd41c4379c49327a629c14b1407))
* preserve valid JSON when comparing computed values ([26a5650](https://github.com/cveld/terraform-blazor-semanticcompare/commit/26a565022f879455fd9206a4e244e06fd8ba58ce))

## [0.3.0](https://github.com/cveld/terraform-blazor-semanticcompare/compare/v0.2.0...v0.3.0) (2026-10-05)


### Features

* add agent API to push a plan into an open page ([b756e07](https://github.com/cveld/terraform-blazor-semanticcompare/commit/b756e072984b77bce8e36bdec78e8f87bb53759b))
* migrate to .NET 10 ([5b76361](https://github.com/cveld/terraform-blazor-semanticcompare/commit/5b763618b824ca146e1ef3832d311d7ec1f0d9fe))
* show why a resource is replaced ([a3d72cd](https://github.com/cveld/terraform-blazor-semanticcompare/commit/a3d72cd054a82068bbfc24fb384f24843cea1008))


### Bug Fixes

* remove unreachable branch in JsonTreeNode ([a02a7df](https://github.com/cveld/terraform-blazor-semanticcompare/commit/a02a7dffbff9b7ed5f414cc178cfd4e8070db1d9))

## [0.2.0](https://github.com/cveld/terraform-blazor-semanticcompare/compare/v0.1.0...v0.2.0) (2026-10-02)


### Features

* register WebMCP load-terraform-plan tool in the viewer ([2f4f73b](https://github.com/cveld/terraform-blazor-semanticcompare/commit/2f4f73b2073663c4b88a21a4903483f1a71522e9))
* show version and build date in page footer ([5aa7700](https://github.com/cveld/terraform-blazor-semanticcompare/commit/5aa770012c52fdffbadc179aa65783dab4f40692))


### Bug Fixes

* match arrays by unique name or id key and show no-difference notice ([5048674](https://github.com/cveld/terraform-blazor-semanticcompare/commit/5048674cf61b26ad17ce1cb738ec0eefe8d9e35e))
