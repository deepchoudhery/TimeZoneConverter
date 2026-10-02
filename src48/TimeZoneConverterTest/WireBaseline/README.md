# Golden wire baseline (test resources)

Byte-exact copies of the **LIVE GOLDEN** net481 wire baseline captured from the original
.NET Framework 4.8.1 WCF host (IIS Express, `http://localhost:50355/TimeZoneConverter.svc`)
by upgrade task `01-validation-environment-and-wire-baseline`, source commit `02bf9304`.

Origin: `.github/upgrades/scenarios/wcf-to-corewcf-upgrade/wcf-baseline/` (`envelopes/`,
`metadata/`, `capture-manifest.json`). They are copies, not output from the migrated host. Do **not** refresh
them from the CoreWCF host: they are the oracle that `EndpointWireTests` checks for Strict XML parity.

- `capture-manifest.json` records the URL, request headers, HTTP status, response headers and
  SHA256 for every file. `EndpointWireTests` checks each file's SHA256 against it before use.
- `.gitattributes` (`* -text`) keeps git from changing line endings. The SoapUI request bodies mix
  CRLF and LF exactly as they were sent.
