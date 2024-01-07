import sys, os 
sys.path.insert(1, os.getenv("NYX_INTERPRETER_BUILD_PATH"))

from spec_lib.graph_spec import *
from spec_lib.data_spec import *
from spec_lib.graph_builder import *
from spec_lib.generators import opts,flags,limits,regex

s = Spec()
s.use_std_lib = False
s.includes.append("\"custom_includes.h\"")
s.includes.append("\"nyx.h\"")
s.interpreter_user_data_type = "fd_state_t*"

d_pos = s.data_u32("pos", generators=[limits(0, (1 << 32) - 1)])
d_button = s.data_u32("button", generators=[limits(0, 5)])
d_key = s.data_u32("key", generators=[limits(0, 134)])

move_code = """
    if (vm->user_data->len > 0){
        uint32_t* data32 = (uint32_t*)vm->user_data->data;
        data32[0] = 0;
        data32[1] = *data_pos;
        vm->user_data->len = sizeof(uint32_t) * 2;
    }
    """
n_move = s.node_type("move", interact=True, data=d_pos, code=move_code)

down_code = """
    if (vm->user_data->len > 0){
        uint32_t* data32 = (uint32_t*)vm->user_data->data;
        data32[0] = 1;
        data32[1] = *data_button;
        vm->user_data->len = sizeof(uint32_t) * 2;
    }
    """
n_down = s.node_type("down", interact=True, data=d_button, code=down_code)

up_code = """
    if (vm->user_data->len > 0){
        uint32_t* data32 = (uint32_t*)vm->user_data->data;
        data32[0] = 2;
        data32[1] = *data_button;
        vm->user_data->len = sizeof(uint32_t) * 2;
    }
    """
n_up = s.node_type("up", interact=True, data=d_button, code=up_code)

key_code = """
    if (vm->user_data->len > 0){
        uint32_t* data32 = (uint32_t*)vm->user_data->data;
        data32[0] = 3;
        data32[1] = *data_key;
        vm->user_data->len = sizeof(uint32_t) * 2;
    }
    """
n_key = s.node_type("key", interact=True, data=d_key, code=key_code)

snapshot_code="""
//hprintf("ASKING TO CREATE SNAPSHOT\\n");
kAFL_hypercall(HYPERCALL_KAFL_CREATE_TMP_SNAPSHOT, 0);
kAFL_hypercall(HYPERCALL_KAFL_USER_FAST_ACQUIRE, 0);
//hprintf("RETURNING FROM SNAPSHOT\\n");
vm->ops_i -= OP_CREATE_TMP_SNAPSHOT_SIZE;
"""
n_close = s.node_type("create_tmp_snapshot", code=snapshot_code)

s.build_interpreter()

import msgpack
serialized_spec = s.build_msgpack()
with open("nyx_net_spec.msgp","wb") as f:
    f.write(msgpack.packb(serialized_spec))
