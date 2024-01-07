#!/bin/bash
PACKER="ext/packer/packer/nyx_packer.py"
CONFIG_GEN="ext/packer/packer/nyx_config_gen.py"
SHAREDIR="packed"

dotnet publish -r linux-x64 -c Release && cp lazerfuzz/bin/Release/net6.0/linux-x64/publish/lazerfuzz target

python3 $PACKER target $SHAREDIR spec instrumentation \
    --purge \
    -spec spec \
    -env "OSU_EXECUTION_MODE=SingleThread OSU_TESTS_NO_TIMEOUT=1" \
    -deps "/lib/x86_64-linux-gnu/libssl.so.1.1 /lib/x86_64-linux-gnu/libcrypto.so.1.1" \
    --delayed_init \
    --fast_reload_mode \
    --debug_stdin_stderr \
    --ignore_ld \
    --no_preload
python3 $CONFIG_GEN $SHAREDIR -m 2048 -n 4294967295 Kernel
