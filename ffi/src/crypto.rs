use crate::FfiResult;
use bitvm::signatures::public::{CompactWots, Wots, Wots16, Wots32, Wots4, Wots64, Wots80};
use std::convert::TryFrom;
use std::os::raw::c_char;

/// Generate a Winternitz secret key
///
/// # Returns
/// FfiResult with 32 random bytes
#[no_mangle]
pub extern "C" fn crypto_generate_winternitz_secret() -> FfiResult {
    let secret = Wots16::generate_secret_key();
    FfiResult::ok(secret)
}

/// Generate a Winternitz public key from secret
///
/// # Arguments
/// * `secret_bytes` - 32-byte secret key
/// * `secret_len` - Length of secret (must be 32)
/// * `message_size` - Message size in bytes (4, 16, 32, 64, or 80)
///
/// # Returns
/// FfiResult with JSON-encoded public key
#[no_mangle]
pub extern "C" fn crypto_winternitz_pubkey_from_secret(
    secret_bytes: *const u8,
    secret_len: usize,
    message_size: u32,
) -> FfiResult {
    if secret_bytes.is_null() {
        return FfiResult::err("Null secret pointer");
    }

    if secret_len != 20 {
        return FfiResult::err("Secret must be 20 bytes");
    }

    let secret = unsafe { std::slice::from_raw_parts(secret_bytes, secret_len) };
    let secret = secret.to_vec();

    let result = match message_size {
        4 => public_key_to_json::<Wots4>(&secret),
        16 => public_key_to_json::<Wots16>(&secret),
        32 => public_key_to_json::<Wots32>(&secret),
        64 => public_key_to_json::<Wots64>(&secret),
        80 => public_key_to_json::<Wots80>(&secret),
        _ => return FfiResult::err("Unsupported message size"),
    };

    match result {
        Ok(bytes) => FfiResult::ok(bytes),
        Err(err) => FfiResult::err(&err),
    }
}

/// Sign a message with Winternitz signature
///
/// # Arguments
/// * `secret_bytes` - 32-byte secret key
/// * `secret_len` - Length of secret
/// * `message_bytes` - Message to sign
/// * `message_len` - Length of message
/// * `message_size` - Expected message size (4, 16, 32, 64, or 80)
///
/// # Returns
/// FfiResult with JSON-encoded signature
#[no_mangle]
pub extern "C" fn crypto_winternitz_sign(
    secret_bytes: *const u8,
    secret_len: usize,
    message_bytes: *const u8,
    message_len: usize,
    message_size: u32,
) -> FfiResult {
    if secret_bytes.is_null() || message_bytes.is_null() {
        return FfiResult::err("Null pointer argument");
    }

    if secret_len != 20 {
        return FfiResult::err("Secret must be 20 bytes");
    }

    if message_len != message_size as usize {
        return FfiResult::err("Message length mismatch");
    }

    let secret = unsafe { std::slice::from_raw_parts(secret_bytes, secret_len) }.to_vec();
    let message = unsafe { std::slice::from_raw_parts(message_bytes, message_len) }.to_vec();

    let result = match message_size {
        4 => signature_to_json::<Wots4>(&secret, message),
        16 => signature_to_json::<Wots16>(&secret, message),
        32 => signature_to_json::<Wots32>(&secret, message),
        64 => signature_to_json::<Wots64>(&secret, message),
        80 => signature_to_json::<Wots80>(&secret, message),
        _ => return FfiResult::err("Unsupported message size"),
    };

    match result {
        Ok(bytes) => FfiResult::ok(bytes),
        Err(err) => FfiResult::err(&err),
    }
}

/// Generate Winternitz signature verification script
///
/// # Arguments
/// * `pubkey_json` - JSON-encoded public key
/// * `message_size` - Message size (4, 16, 32, 64, or 80)
/// * `compact` - Use compact signature variant
///
/// # Returns
/// FfiResult with script bytecode
#[no_mangle]
pub extern "C" fn crypto_winternitz_checksig_script(
    pubkey_json: *const c_char,
    message_size: u32,
    compact: bool,
) -> FfiResult {
    if pubkey_json.is_null() {
        return FfiResult::err("Null pubkey pointer");
    }

    let pubkey_str = unsafe {
        match std::ffi::CStr::from_ptr(pubkey_json).to_str() {
            Ok(s) => s,
            Err(_) => return FfiResult::err("Invalid UTF-8 in pubkey JSON"),
        }
    };

    let pubkey_entries: Vec<Vec<u8>> = match serde_json::from_str(pubkey_str) {
        Ok(entries) => entries,
        Err(e) => return FfiResult::err(&format!("JSON parse error: {}", e)),
    };

    let result = match message_size {
        4 => checksig_script::<Wots4>(pubkey_entries, compact),
        16 => checksig_script::<Wots16>(pubkey_entries, compact),
        32 => checksig_script::<Wots32>(pubkey_entries, compact),
        64 => checksig_script::<Wots64>(pubkey_entries, compact),
        80 => checksig_script::<Wots80>(pubkey_entries, compact),
        _ => return FfiResult::err("Unsupported message size"),
    };

    match result {
        Ok(bytes) => FfiResult::ok(bytes),
        Err(err) => FfiResult::err(&err),
    }
}

fn public_key_to_json<T: Wots>(secret: &[u8]) -> Result<Vec<u8>, String> {
    let public_key = T::generate_public_key(secret);
    let public_key_bytes: Vec<Vec<u8>> = public_key.as_ref().iter().map(|e| e.to_vec()).collect();
    serde_json::to_vec(&public_key_bytes).map_err(|e| e.to_string())
}

fn signature_to_json<T: Wots>(secret: &[u8], message: Vec<u8>) -> Result<Vec<u8>, String> {
    let message = T::Message::try_from(message)
        .map_err(|_| "Invalid message length for message size".to_string())?;
    let signature = T::sign(secret, &message);
    let signature_bytes: Vec<Vec<u8>> = signature.as_ref().iter().map(|e| e.to_vec()).collect();
    serde_json::to_vec(&signature_bytes).map_err(|e| e.to_string())
}

fn checksig_script<T: Wots + CompactWots>(
    pubkey_entries: Vec<Vec<u8>>,
    compact: bool,
) -> Result<Vec<u8>, String> {
    let pubkey_vec: Vec<[u8; 20]> = pubkey_entries
        .into_iter()
        .map(|entry| {
            if entry.len() != 20 {
                return Err("Public key entries must be 20 bytes".to_string());
            }
            let mut bytes = [0u8; 20];
            bytes.copy_from_slice(&entry);
            Ok(bytes)
        })
        .collect::<Result<Vec<_>, _>>()?;

    let public_key = T::PublicKey::try_from(pubkey_vec)
        .map_err(|_| "Invalid public key length for message size".to_string())?;

    let script = if compact {
        T::compact_checksig_verify(&public_key)
    } else {
        T::checksig_verify(&public_key)
    };

    Ok(script.compile().to_bytes())
}
