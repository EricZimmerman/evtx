# Samples from omerbenamram/evtx

From https://github.com/omerbenamram/EVTX/tree/master/samples (MIT or Apache-2.0), gzipped; the tests decompress them.
The expected record counts are what that parser (evtx_dump 0.12.3) finds.

- `security.evtx`, `system.evtx`, `sysmon.evtx`, `new-user-security.evtx`, `issue_201.evtx`
- `Archive-ForwardedEvents-test.evtx`: Windows Event Forwarding (records without templates, RenderingInfo); EvtxECmd issue #15
- `MSExchange_Management_wec.evtx`: a Windows Event Collector log
- `sample_with_a_bad_chunk_magic.evtx`, `security_bad_string_cache.evtx`, `sample-with-zero-data-size-event.evtx`,
  `sample-with-irregular-bool-values.evtx`, `Security_with_size_t.evtx`, `Application_no_crc32.evtx`
- `2-system-Security-dirty.evtx`: a dirty log
