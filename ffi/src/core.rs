use crate::FfiResult;
use bitcoin::ScriptBuf;
use bitvm::{execute_raw_script_with_inputs, execute_script_buf, ExecuteInfo};
use serde::Serialize;
use std::ffi::CStr;
use std::os::raw::c_char;
use std::slice;

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ExecStatsDto {
    max_stack_size: usize,
    max_alt_stack_size: usize,
    opcode_count: usize,
    start_validation_weight: i64,
    validation_weight: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ExecuteInfoDto {
    success: bool,
    error: Option<String>,
    final_stack: String,
    remaining_script: String,
    last_opcode: Option<String>,
    stats: ExecStatsDto,
}

fn execution_info_to_json(info: ExecuteInfo) -> FfiResult {
    let stats = ExecStatsDto {
        max_stack_size: info.stats.max_nb_stack_items,
        max_alt_stack_size: 0,
        opcode_count: info.stats.opcode_count,
        start_validation_weight: info.stats.start_validation_weight,
        validation_weight: info.stats.validation_weight,
    };

    let dto = ExecuteInfoDto {
        success: info.success,
        error: info.error.map(|err| format!("{:?}", err)),
        final_stack: format!("{:4}", info.final_stack),
        remaining_script: info.remaining_script,
        last_opcode: info.last_opcode.map(|op| format!("{:?}", op)),
        stats,
    };

    match serde_json::to_vec(&dto) {
        Ok(data) => FfiResult::ok(data),
        Err(e) => FfiResult::err(&format!("JSON serialization error: {}", e)),
    }
}

/// Execute a Bitcoin script and return the result as JSON
#[no_mangle]
pub extern "C" fn bitvm_execute_script(script_bytes: *const u8, script_len: usize) -> FfiResult {
    if script_bytes.is_null() && script_len != 0 {
        return FfiResult::err("Null script bytes pointer with non-zero length");
    }

    let script_slice = unsafe { slice::from_raw_parts(script_bytes, script_len) };
    let script_buf = ScriptBuf::from_bytes(script_slice.to_vec());

    let result = execute_script_buf(script_buf);
    execution_info_to_json(result)
}

/// Execute a script with witness inputs
#[no_mangle]
pub extern "C" fn bitvm_execute_script_with_witness(
    script_bytes: *const u8,
    script_len: usize,
    witness_json: *const c_char,
) -> FfiResult {
    if (script_bytes.is_null() && script_len != 0) || witness_json.is_null() {
        return FfiResult::err("Null pointer argument");
    }

    let script_slice = unsafe { slice::from_raw_parts(script_bytes, script_len) };
    let witness_cstr = unsafe { CStr::from_ptr(witness_json) };

    let witness_str = match witness_cstr.to_str() {
        Ok(s) => s,
        Err(_) => return FfiResult::err("Invalid UTF-8 in witness JSON"),
    };

    let witness: Vec<Vec<u8>> = match serde_json::from_str(witness_str) {
        Ok(w) => w,
        Err(e) => return FfiResult::err(&format!("JSON parse error: {}", e)),
    };

    let result = execute_raw_script_with_inputs(script_slice.to_vec(), witness);
    execution_info_to_json(result)
}

/// Generate a SHA256 script for the given message length
#[no_mangle]
pub extern "C" fn bitvm_sha256_script(message_len: usize) -> FfiResult {
    use bitvm::hash::sha256::sha256;

    let script = sha256(message_len);
    let script_bytes = script.compile().to_bytes();

    FfiResult::ok(script_bytes)
}

/// Generate a SHA256 script for 32-byte input
#[no_mangle]
pub extern "C" fn bitvm_sha256_32bytes_script() -> FfiResult {
    use bitvm::hash::sha256::sha256_32bytes;

    let script = sha256_32bytes();
    let script_bytes = script.compile().to_bytes();

    FfiResult::ok(script_bytes)
}

/// Generate a BLAKE3 script
#[no_mangle]
pub extern "C" fn bitvm_blake3_script(message_len: usize) -> FfiResult {
    use bitvm::hash::blake3::blake3_compute_script;

    let script = blake3_compute_script(message_len);
    let script_bytes = script.compile().to_bytes();

    FfiResult::ok(script_bytes)
}

/// Push a u32 value onto the stack
#[no_mangle]
pub extern "C" fn bitvm_u32_push_script(value: u32) -> FfiResult {
    use bitvm::u32::u32_std::u32_push;

    let script = u32_push(value);
    let script_bytes = script.compile().to_bytes();

    FfiResult::ok(script_bytes)
}

/// Generate u32 equality verification script
#[no_mangle]
pub extern "C" fn bitvm_u32_equalverify_script() -> FfiResult {
    use bitvm::u32::u32_std::u32_equalverify;

    let script = u32_equalverify();
    let script_bytes = script.compile().to_bytes();

    FfiResult::ok(script_bytes)
}
