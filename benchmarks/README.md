# Serialization Benchmarks

Measures OCI manifest/index JSON serialization and deserialization performance.

## Run

```bash
dotnet run --project benchmarks/SerializationBenchmark.csproj -- [baseline|pr|auto]
```

- `baseline` — uses `System.Text.Json` directly (upstream behavior before Go-compatible encoding)
- `pr` — uses `OciJsonSerializer` with Go-compatible escaping + optimizations
- `auto` (default) — detects which path is available

## What's Measured

- **Serialize**: manifest (5 & 50 annotations), index (10 manifests)
- **Deserialize sync**: same payloads from `byte[]`
- **DeserializeAsync**: same payloads from `MemoryStream`
- Reports avg, median, and p95 latency in microseconds
