import os

# need to run rust_fuzzer_debug first
# cargo run --release -- -s ../../../../fuzz/osueditor/packed -d /tmp/workdir/corpus/normal/ -t /tmp/workdir/corpus/normal_reproducible/

CORPUS_PATH = "/tmp/workdir/corpus/normal_reproducible/"

def packet(data):
    for x in data:
        print(f"{hex(ord(x))}, ", end="")

print("namespace lazerfuzz;\n\ninternal partial class Program\n{\n    static private byte[][] warmupInputs = new byte[][]\n    {")
for x in os.listdir(CORPUS_PATH):
    print("        new byte[] { ", end="")
    exec(open(os.path.join(CORPUS_PATH, x)).read())
    print("},")
print("    };\n}")