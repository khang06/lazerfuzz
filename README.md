# lazerfuzz
Experimental osu!lazer UI fuzz harness powered by [Nyx](https://nyx-fuzz.com/)

## Usage
1. Install Rust and libSSL 1.1.1 (TODO: what build dependencies does QEMU need?)
2. Clone the repo recursively
3. Compile QEMU-Nyx
```
cd ext/QEMU-Nyx
./compile_qemu_nyx.sh lto
cd -
```
4. Copy `osu.Game.dll` and `osu.Game.Rulesets.Osu.dll` to the root of the repository
5. Install SharpFuzz and use it to instrument the DLLs
```
dotnet tool install --global SharpFuzz.CommandLine
sharpfuzz osu.Game.dll && sharpfuzz osu.Game.Rulesets.Osu.dll
```
6. Build the fuzz harness and pack it into a Nyx sharedir
```
./pack.sh
```
7. Start fuzzing!
```
cd ext/spec-fuzzer/rust_fuzzer
cargo run --release -- -s ../../../packed --threads $(nproc) -p aggressive
```

## Reproducing Crashes
1. Convert the generated crashing testcases into Python scripts
```
cd ext/spec-fuzzer/rust_fuzzer_debug
cargo run --release -- -s ../../../packed -d /tmp/workdir/corpus/crash -t /tmp/workdir/corpus/crash_reproducible
cd -
```
2. Replay the crashes
```
python3 ext/packer/packer/nyx_net_payload_executor.py /tmp/workdir/corpus/crash_reproducible/cnt_<num>.py stdout | ./target -
```
The recorded input sequence will also be logged in the console in a way that allows them to be pasted directly into a test harness.

## Warmup
It's possible to run some generated inputs before the fuzzing loop starts to preload anything that gets lazy-loaded:

1. Convert the generated testcases into Python scripts
```
cd ext/spec-fuzzer/rust_fuzzer_debug
cargo run --release -- -s ../../../packed -d /tmp/workdir/corpus/normal -t /tmp/workdir/corpus/normal_reproducible
cd -
```
2. Write all of the testcases into `Program.Warmup.cs`
```
python3 convert_warmup.py > lazerfuzz/Program.Warmup.cs
```

## Issues
* It's pretty slow, but it's still way better than not using a snapshot fuzzer. My Ryzen 3600 can only get 50-100 execs/sec on all 12 threads.
* A custom osu!framework needs to be used because BASS only returns the "No sound" device, but not the "Default" one. This causes vanilla osu!framework to freak out and fail an assert. (why does this work on Github CI?)
* Certain testcases can crash when used as warmup cases because the glyph cache gets deleted while they're running. (race condition?)
* The fuzzer will sometimes hang right after spawning its threads.
