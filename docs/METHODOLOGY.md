# Workload methodology and sources

These workloads are independent measurements. No proprietary CrystalDiskMark, AS SSD or FurMark score is calculated.

## Storage

DiskSpd 2.3 x64 is the native engine. Default tests are SEQ1M Q8T1, SEQ1M Q1T1, RND4K Q32T1 and RND4K Q1T1. NVMe adds SEQ128K Q32T1 and RND4K Q32T16. Queue depth is **per thread**, so Q32T16 permits 512 outstanding requests. Custom block size, queue depth, threads, duration and repetitions are explicit. MB/s uses decimal millions; file size uses MiB. IOPS is operations divided by measured duration. Latency is the measured aggregate read/write average in microseconds, parsed from `TimeSpan/Latency/AverageReadMilliseconds` or `AverageWriteMilliseconds`. Missing result fields fail validation rather than becoming zero.

Read and write phases run separately with unbuffered file I/O (`-Su`); device write caching is not disabled. A preallocated temporary file contains a high-entropy 1 MiB pattern. Writes use a pre-generated 1 MiB random source buffer, avoiding per-operation random-generation CPU overhead. Default and NVMe profiles include one unscored trial for each direction, then retain the fastest measured trial with its associated IOPS and latency. AS SSD inspired profiles omit that extra trial.

AS SSD inspired tests use sequential 1 MiB Q1T1, random 4 KiB Q1T1, random 4 KiB Q1T64, and a distinct random 512-byte Q1T1 access-time workload. Access time covers the temporary file only. It cannot reproduce a proprietary full-device access-time method without accessing unrelated data. Never interpret it as a byte-for-byte reproduction of AS SSD. The reference author's site was consulted but unavailable during this audit; exact unpublished details were not inferred.

Copy tests use generated source and destination files on the same selected physical disk. ISO uses one large file, Program 64 KiB files, and Game a mixture of 8 MiB and 128 KiB files. Copy time includes reads, writes and destination `Flush(true)`. OS source caching can affect results; these are filesystem copy measurements and not unbuffered throughput. Source preparation is excluded from the reported copy time.

Compression tests measure separate read/write throughput at 0, 25, 50, 75 and 100% zero content in a 1 MiB source pattern. The file is refilled before each point; reads precede writes. The graph displays measured points, not an estimated score. This tests controller sensitivity to compressibility; it does not enable filesystem compression.

DiskSpd is given only a newly created file under a GUID-named folder. Disk number, model, Windows serial, capacity, bus and native SMART serial (when readable) are checked again before file creation. Offline, read-only, virtual, RAID, Spaces, iSCSI and unknown devices are rejected. Mapping requires a fixed, mounted, supported filesystem with sufficient free space. No raw device writes are supported. Forced termination may leave that run's temporary folder; no broad cleanup of user volumes is attempted.

## GPU

Native OpenGL ray-marches a procedural torus with 128 ray steps and a 48-iteration surface shader. An offscreen RGBA8 framebuffer preserves selected render resolution even when the display is smaller. The result is blitted to the window. FPS includes GPU completion and presentation. Minimum/maximum refer to measured one-second intervals; average is total completed frames divided by elapsed time. Windowed and fullscreen presentation use the same offscreen workload.

Temperature is sampled through NVIDIA SMI; invalid or lost telemetry stops rendering. The render thread checks telemetry freshness, cancellation, duration and thermal cutoff; the parent also kills a silent worker. The renderer must be NVIDIA and exactly one NVIDIA adapter must have valid telemetry. Arbitrary multi-adapter selection is not implemented because consumer OpenGL adapter affinity cannot be assumed. Escape, closing the renderer and the main Stop button cancel a run.

## Upstream references

- Microsoft DiskSpd parameters: https://github.com/microsoft/diskspd/wiki/Command-line-and-parameters
- Official result parser: https://github.com/microsoft/diskspd/blob/master/Process-DiskSpd.ps1
- Official AS SSD reference site (unreachable during audit): https://www.alex-is.de/PHP/fusion/infusions/downloads/downloads.php?cat_id=4&download_id=9
- LibreHardwareMonitor 0.9.6 source/API: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6
- smartctl 7.5 manual is included in the downloaded upstream package under `tools/smart-package/doc`.

Compare against reference applications only with matching file size, cache policy, thread count, total queue depth, duration, data pattern and device temperature. No reference comparison is marked passed until it is actually performed.

Storage temperature is recorded before preparation and between measured phases. When NVMe temperature is available, a reading of 70 C or above stops further phases. Unavailable telemetry is explicitly reported; this is a between-phase guard, not a continuous storage controller shutdown mechanism.
