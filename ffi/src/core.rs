use crate::FfiResult;
use bitvm::treepp::script;
use bitvm::{execute_script, execute_script_with_inputs};
use std::ffi::CStr;
use std::os::raw::c_char;
use std::slice;

/// Execute a Bitcoin script and return the result as JSON
#[no_mangle]
pub extern "C" fn bitvm_execute_script(script_bytes: *const u8, script_len: usize) -> FfiResult {
    if script_bytes.is_null() {
        return FfiResult::err("Null script bytes pointer");
    }

    let script_slice = unsafe { slice::from_raw_parts(script_bytes, script_len) };

    let script = script! {
        for byte in script_slice {
            { *byte as i64 }
        }
    };

    let result = execute_script(script);

    // Manual JSON serialization since ExecuteInfo doesn't implement Serialize
    let json = format!(
        r#"{{"success":{},"error":null,"final_stack":"","remaining_script":"{}"}}"#,
        result.success,
        result.remaining_script.replace("\"", "\\\"")
    );

    FfiResult::ok(json.into_bytes())
}

/// Execute a script with witness inputs
#[no_mangle]
pub extern "C" fn bitvm_execute_script_with_witness(
    script_bytes: *const u8,
    script_len: usize,
    witness_json: *const c_char,
) -> FfiResult {
    if script_bytes.is_null() || witness_json.is_null() {
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

    let script = script! {
        for byte in script_slice {
            { *byte as i64 }
        }
    };

    let result = execute_script_with_inputs(script, witness);

    // Manual JSON serialization
    let json = format!(
        r#"{{"success":{},"error":null,"final_stack":"","remaining_script":"{}"}}"#,
        result.success,
        result.remaining_script.replace("\"", "\\\"")
    );

    FfiResult::ok(json.into_bytes())
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
