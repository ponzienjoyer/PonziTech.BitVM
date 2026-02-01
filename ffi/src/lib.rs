#![allow(unsafe_code)]
#![deny(warnings)]

pub mod core;
pub mod crypto;

use std::ffi::CString;
use std::os::raw::c_char;
use std::ptr;

/// FFI Version string - must be null-terminated for C compatibility
static VERSION_CSTRING: std::sync::LazyLock<CString> = std::sync::LazyLock::new(|| {
    CString::new(env!("CARGO_PKG_VERSION")).expect("Version string contains null")
});

/// Returns the FFI version as a C string
/// The caller is responsible for freeing the returned pointer using bitvm_free_string()
#[no_mangle]
pub extern "C" fn bitvm_ffi_version() -> *mut c_char {
    VERSION_CSTRING.as_ptr() as *mut c_char
}

/// Initialize the BitVM FFI layer
/// Returns 0 on success, non-zero on error
#[no_mangle]
pub extern "C" fn bitvm_init() -> i32 {
    // Initialize any required state
    // For now, just return success
    0
}

/// Cleanup the BitVM FFI layer
#[no_mangle]
pub extern "C" fn bitvm_cleanup() {
    // Cleanup any allocated resources
}

/// Free a string allocated by the FFI layer
#[no_mangle]
pub extern "C" fn bitvm_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

/// Free a byte buffer allocated by the FFI layer
#[no_mangle]
pub extern "C" fn bitvm_free_bytes(ptr: *mut u8, len: usize) {
    if !ptr.is_null() {
        unsafe {
            let _ = Vec::from_raw_parts(ptr, len, len);
        }
    }
}

/// Helper to convert a Rust string to a C string
fn string_to_cstring(s: &str) -> *mut c_char {
    match CString::new(s) {
        Ok(cs) => cs.into_raw(),
        Err(_) => ptr::null_mut(),
    }
}

/// Helper to convert a byte vector to a C buffer
fn vec_to_cbuffer(vec: Vec<u8>) -> (*mut u8, usize) {
    let mut boxed = vec.into_boxed_slice();
    let ptr = boxed.as_mut_ptr();
    let len = boxed.len();
    std::mem::forget(boxed);
    (ptr, len)
}

/// Result structure for FFI operations
#[repr(C)]
pub struct FfiResult {
    pub success: bool,
    pub data: *mut u8,
    pub data_len: usize,
    pub error_message: *mut c_char,
}

impl FfiResult {
    pub fn ok(data: Vec<u8>) -> Self {
        let (ptr, len) = vec_to_cbuffer(data);
        FfiResult {
            success: true,
            data: ptr,
            data_len: len,
            error_message: ptr::null_mut(),
        }
    }

    pub fn err(msg: &str) -> Self {
        FfiResult {
            success: false,
            data: ptr::null_mut(),
            data_len: 0,
            error_message: string_to_cstring(msg),
        }
    }
}

/// Frees a FfiResult structure
#[no_mangle]
pub extern "C" fn bitvm_free_result(result: FfiResult) {
    if !result.data.is_null() {
        bitvm_free_bytes(result.data, result.data_len);
    }
    if !result.error_message.is_null() {
        bitvm_free_string(result.error_message);
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_version() {
        let version_ptr = bitvm_ffi_version();
        assert!(!version_ptr.is_null());
    }

    #[test]
    fn test_init_cleanup() {
        assert_eq!(bitvm_init(), 0);
        bitvm_cleanup();
    }
}
