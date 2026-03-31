// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using OrasProject.Oras.Oci;
using Index = OrasProject.Oras.Oci.Index;

// ── Configuration ──────────────────────────────────────────
const int warmupIterations = 100;
const int iterations = 5000;

// ── Build test manifests ───────────────────────────────────
var smallAnnotations = new Dictionary<string, string>
{
    ["org.opencontainers.image.title"] = "hello.txt",
    ["org.opencontainers.image.created"] = "2026-01-15T10:30:00Z"
};

var annotationsWithPlus = new Dictionary<string, string>
{
    ["org.opencontainers.image.title"] = "hello.txt",
    ["org.opencontainers.image.ref.name"] =
        "application/vnd.oci.image.manifest.v1+json",
    ["org.example.custom+type"] = "value+with+plus",
    ["org.example.html"] = "<div>&content</div>",
    ["org.example.unicode"] = "line\u2028separator"
};

var largeAnnotations = new Dictionary<string, string>();
for (int i = 0; i < 50; i++)
{
    largeAnnotations[$"org.example.key{i}+annotation"] =
        $"value-{i}-with+plus-and-<html>&special-chars";
}

var manifest = new Manifest
{
    SchemaVersion = 2,
    MediaType = OrasProject.Oras.Oci.MediaType.ImageManifest,
    ArtifactType = "application/vnd.example.artifact+type",
    Config = new Descriptor
    {
        MediaType = OrasProject.Oras.Oci.MediaType.EmptyJson,
        Digest =
            "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a",
        Size = 2
    },
    Layers = new List<Descriptor>
    {
        new()
        {
            MediaType = "application/vnd.oci.image.layer.v1.tar+gzip",
            Digest =
                "sha256:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            Size = 1024,
            Annotations = smallAnnotations
        }
    },
    Annotations = annotationsWithPlus
};

var manifestLargeAnnotations = new Manifest
{
    SchemaVersion = 2,
    MediaType = OrasProject.Oras.Oci.MediaType.ImageManifest,
    Config = new Descriptor
    {
        MediaType = OrasProject.Oras.Oci.MediaType.EmptyJson,
        Digest =
            "sha256:44136fa355b3678a1146ad16f7e8649e94fb4fc21caaff8a",
        Size = 2
    },
    Layers = new List<Descriptor>(),
    Annotations = largeAnnotations
};

var indexManifests = new List<Descriptor>();
for (int i = 0; i < 10; i++)
{
    indexManifests.Add(new Descriptor
    {
        MediaType = OrasProject.Oras.Oci.MediaType.ImageManifest,
        Digest =
            $"sha256:{i:d64}",
        Size = 512 + i,
        Annotations = new Dictionary<string, string>
        {
            [$"org.example+platform{i}"] = $"linux/amd64+v{i}"
        }
    });
}

var index = new Index
{
    SchemaVersion = 2,
    MediaType = OrasProject.Oras.Oci.MediaType.ImageIndex,
    Manifests = indexManifests,
    Annotations = annotationsWithPlus
};

// ── Determine mode ─────────────────────────────────────────
// "baseline" uses JsonSerializer directly (upstream/main behavior)
// "pr" uses OciJsonSerializer (PR branch behavior)
var mode = args.Length > 0 ? args[0] : "auto";

if (mode == "auto")
{
    // Detect whether OciJsonSerializer is available
    var ociType = Type.GetType(
        "OrasProject.Oras.Serialization.OciJsonSerializer,"
        + " OrasProject.Oras");
    mode = ociType != null ? "pr" : "baseline";
}

Console.WriteLine($"=== Serialization Benchmark ({mode}) ===");
Console.WriteLine(
    $"Warmup: {warmupIterations}, Iterations: {iterations}");
Console.WriteLine();

// ── Benchmark helpers ──────────────────────────────────────
static (double avgUs, double medianUs, double p95Us) Measure(
    Action action, int warmup, int iters)
{
    // Warmup
    for (int i = 0; i < warmup; i++) action();

    var times = new double[iters];
    var sw = new Stopwatch();
    for (int i = 0; i < iters; i++)
    {
        sw.Restart();
        action();
        sw.Stop();
        times[i] = sw.Elapsed.TotalMicroseconds;
    }
    Array.Sort(times);
    var avg = times.Average();
    var median = times[iters / 2];
    var p95 = times[(int)(iters * 0.95)];
    return (avg, median, p95);
}

static (double avgUs, double medianUs, double p95Us) MeasureAsync(
    Func<Task> action, int warmup, int iters)
{
    // Warmup
    for (int i = 0; i < warmup; i++) action().GetAwaiter().GetResult();

    var times = new double[iters];
    var sw = new Stopwatch();
    for (int i = 0; i < iters; i++)
    {
        sw.Restart();
        action().GetAwaiter().GetResult();
        sw.Stop();
        times[i] = sw.Elapsed.TotalMicroseconds;
    }
    Array.Sort(times);
    var avg = times.Average();
    var median = times[iters / 2];
    var p95 = times[(int)(iters * 0.95)];
    return (avg, median, p95);
}

void Report(string name, (double avg, double median, double p95) r)
{
    Console.WriteLine(
        $"  {name,-45} avg={r.avg,8:F1}µs  "
        + $"med={r.median,8:F1}µs  p95={r.p95,8:F1}µs");
}

// ── Run benchmarks ─────────────────────────────────────────
if (mode == "baseline")
{
    RunBaseline();
}
else
{
    RunPr();
}

void RunBaseline()
{
    Console.WriteLine("── Serialize (JsonSerializer.SerializeToUtf8Bytes) ──");
    Report("Manifest (5 annotations, +chars)",
        Measure(() => JsonSerializer.SerializeToUtf8Bytes(manifest),
            warmupIterations, iterations));
    Report("Manifest (50 annotations, +chars)",
        Measure(
            () => JsonSerializer.SerializeToUtf8Bytes(manifestLargeAnnotations),
            warmupIterations, iterations));
    Report("Index (10 manifests, annotations)",
        Measure(() => JsonSerializer.SerializeToUtf8Bytes(index),
            warmupIterations, iterations));

    Console.WriteLine();
    Console.WriteLine("── Deserialize (JsonSerializer.Deserialize<T> byte[]) ──");
    var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
    var largeBytes =
        JsonSerializer.SerializeToUtf8Bytes(manifestLargeAnnotations);
    var indexBytes = JsonSerializer.SerializeToUtf8Bytes(index);

    Report("Manifest (5 annotations)",
        Measure(() => JsonSerializer.Deserialize<Manifest>(manifestBytes),
            warmupIterations, iterations));
    Report("Manifest (50 annotations)",
        Measure(
            () => JsonSerializer.Deserialize<Manifest>(largeBytes),
            warmupIterations, iterations));
    Report("Index (10 manifests)",
        Measure(() => JsonSerializer.Deserialize<Index>(indexBytes),
            warmupIterations, iterations));

    Console.WriteLine();
    Console.WriteLine(
        "── DeserializeAsync (JsonSerializer.DeserializeAsync<T>) ──");
    Report("Manifest (5 annotations)",
        MeasureAsync(async () =>
        {
            using var ms = new MemoryStream(manifestBytes);
            await JsonSerializer.DeserializeAsync<Manifest>(ms);
        }, warmupIterations, iterations));
    Report("Manifest (50 annotations)",
        MeasureAsync(async () =>
        {
            using var ms = new MemoryStream(largeBytes);
            await JsonSerializer.DeserializeAsync<Manifest>(ms);
        }, warmupIterations, iterations));
    Report("Index (10 manifests)",
        MeasureAsync(async () =>
        {
            using var ms = new MemoryStream(indexBytes);
            await JsonSerializer.DeserializeAsync<Index>(ms);
        }, warmupIterations, iterations));

    Console.WriteLine();
    Console.WriteLine("── Payload sizes ──");
    Console.WriteLine($"  Manifest (5 ann):  {manifestBytes.Length} bytes");
    Console.WriteLine($"  Manifest (50 ann): {largeBytes.Length} bytes");
    Console.WriteLine($"  Index (10 mfst):   {indexBytes.Length} bytes");
}

void RunPr()
{
    // Use reflection to call OciJsonSerializer since it's internal
    var asm = typeof(Manifest).Assembly;
    var serType = asm.GetType(
        "OrasProject.Oras.Serialization.OciJsonSerializer")!;
    var serMethod = serType.GetMethod(
        "SerializeToUtf8Bytes",
        System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.NonPublic)!;
    var deserByteMethod = serType.GetMethods(
        System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.NonPublic)
        .First(m => m.Name == "Deserialize"
            && m.GetParameters().Length == 1
            && m.GetParameters()[0].ParameterType == typeof(byte[]));
    var deserAsyncMethod = serType.GetMethod(
        "DeserializeAsync",
        System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.NonPublic)!;

    byte[] Serialize<T>(T value) =>
        (byte[])serMethod.MakeGenericMethod(typeof(T))
            .Invoke(null, [value])!;

    T? Deserialize<T>(byte[] bytes) =>
        (T?)deserByteMethod.MakeGenericMethod(typeof(T))
            .Invoke(null, [bytes]);

    async Task<T?> DeserializeAsync<T>(Stream stream)
    {
        var task = (Task<T?>)deserAsyncMethod
            .MakeGenericMethod(typeof(T))
            .Invoke(null, [stream, CancellationToken.None])!;
        return await task;
    }

    Console.WriteLine(
        "── Serialize (OciJsonSerializer.SerializeToUtf8Bytes) ──");
    Report("Manifest (5 annotations, +chars)",
        Measure(() => Serialize(manifest),
            warmupIterations, iterations));
    Report("Manifest (50 annotations, +chars)",
        Measure(() => Serialize(manifestLargeAnnotations),
            warmupIterations, iterations));
    Report("Index (10 manifests, annotations)",
        Measure(() => Serialize(index),
            warmupIterations, iterations));

    Console.WriteLine();
    Console.WriteLine(
        "── Deserialize (OciJsonSerializer.Deserialize<T> byte[]) ──");
    var manifestBytes = Serialize(manifest);
    var largeBytes = Serialize(manifestLargeAnnotations);
    var indexBytes = Serialize(index);

    Report("Manifest (5 annotations)",
        Measure(() => Deserialize<Manifest>(manifestBytes),
            warmupIterations, iterations));
    Report("Manifest (50 annotations)",
        Measure(() => Deserialize<Manifest>(largeBytes),
            warmupIterations, iterations));
    Report("Index (10 manifests)",
        Measure(() => Deserialize<Index>(indexBytes),
            warmupIterations, iterations));

    Console.WriteLine();
    Console.WriteLine(
        "── DeserializeAsync (OciJsonSerializer.DeserializeAsync<T>) ──");
    Report("Manifest (5 annotations)",
        MeasureAsync(async () =>
        {
            using var ms = new MemoryStream(manifestBytes);
            await DeserializeAsync<Manifest>(ms);
        }, warmupIterations, iterations));
    Report("Manifest (50 annotations)",
        MeasureAsync(async () =>
        {
            using var ms = new MemoryStream(largeBytes);
            await DeserializeAsync<Manifest>(ms);
        }, warmupIterations, iterations));
    Report("Index (10 manifests)",
        MeasureAsync(async () =>
        {
            using var ms = new MemoryStream(indexBytes);
            await DeserializeAsync<Index>(ms);
        }, warmupIterations, iterations));

    Console.WriteLine();
    Console.WriteLine("── Payload sizes ──");
    Console.WriteLine($"  Manifest (5 ann):  {manifestBytes.Length} bytes");
    Console.WriteLine($"  Manifest (50 ann): {largeBytes.Length} bytes");
    Console.WriteLine($"  Index (10 mfst):   {indexBytes.Length} bytes");
}
