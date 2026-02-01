use crate::{string_to_cstring, FfiResult};
use std::ffi::CStr;
use std::os::raw::c_char;

/// Create a depositor context
/// 
/// # Arguments
/// * `network` - Network name ("mainnet", "testnet", "signet", "regtest")
/// * `depositor_secret` - Depositor secret key (hex or WIF)
/// * `verifier_public_keys_json` - JSON array of verifier public keys
/// 
/// # Returns
/// FfiResult with JSON-encoded context on success
#[no_mangle]
pub extern "C" fn bridge_create_depositor_context(
    network: *const c_char,
    depositor_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    if network.is_null() || depositor_secret.is_null() || verifier_public_keys_json.is_null() {
        return FfiResult::err("Null pointer argument");
    }

    let network_str = unsafe {
        match CStr::from_ptr(network).to_str() {
            Ok(s) => s,
            Err(_) => return FfiResult::err("Invalid UTF-8 in network string"),
        }
    };

    let secret_str = unsafe {
        match CStr::from_ptr(depositor_secret).to_str() {
            Ok(s) => s,
            Err(_) => return FfiResult::err("Invalid UTF-8 in secret"),
        }
    };

    let verifier_keys_str = unsafe {
        match CStr::from_ptr(verifier_public_keys_json).to_str() {
            Ok(s) => s,
            Err(_) => return FfiResult::err("Invalid UTF-8 in verifier keys"),
        }
    };

    let network = match network_str {
        "mainnet" => bitcoin::Network::Bitcoin,
        "testnet" => bitcoin::Network::Testnet,
        "signet" => bitcoin::Network::Signet,
        "regtest" => bitcoin::Network::Regtest,
        _ => return FfiResult::err("Invalid network"),
    };

    let verifier_keys: Vec<String> = match serde_json::from_str(verifier_keys_str) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&format!("JSON parse error: {}", e)),
    };

    // TODO: Parse public keys and create context
    // For now, return a placeholder
    let context_json = format!(
        r#"{{"network":"{}","depositor_secret_set":true,"verifier_count":{}}}"#,
        network_str,
        verifier_keys.len()
    );

    FfiResult::ok(context_json.into_bytes())
}

/// Create an operator context
/// 
/// # Arguments
/// * `network` - Network name
/// * `operator_secret` - Operator secret key
/// * `verifier_public_keys_json` - JSON array of verifier public keys
/// 
/// # Returns
/// FfiResult with JSON-encoded context on success
#[no_mangle]
pub extern "C" fn bridge_create_operator_context(
    network: *const c_char,
    operator_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    // Similar to depositor context creation
    FfiResult::err("Not yet implemented")
}

/// Create a verifier context
/// 
/// # Arguments
/// * `network` - Network name
/// * `verifier_secret` - Verifier secret key
/// * `verifier_public_keys_json` - JSON array of all verifier public keys (including this one)
/// 
/// # Returns
/// FfiResult with JSON-encoded context on success
#[no_mangle]
pub extern "C" fn bridge_create_verifier_context(
    network: *const c_char,
    verifier_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    // Similar to above
    FfiResult::err("Not yet implemented")
}

/// Create a peg-in graph
/// 
/// # Arguments
/// * `context_json` - JSON-encoded depositor context
/// * `deposit_txid` - Deposit transaction ID (hex)
/// * `deposit_vout` - Deposit output index
/// * `deposit_amount` - Deposit amount in satoshis
/// * `evm_address` - Destination EVM address
/// 
/// # Returns
/// FfiResult with JSON-encoded PegInGraph on success
#[no_mangle]
pub extern "C" fn bridge_create_peg_in_graph(
    context_json: *const c_char,
    deposit_txid: *const c_char,
    deposit_vout: u32,
    deposit_amount: u64,
    evm_address: *const c_char,
) -> FfiResult {
    FfiResult::err("Not yet implemented")
}

/// Get peg-in graph status for depositor
/// 
/// # Arguments
/// * `graph_json` - JSON-encoded PegInGraph
/// * `esplora_url` - Esplora API URL
/// 
/// # Returns
/// FfiResult with JSON-encoded status
#[no_mangle]
pub extern "C" fn bridge_get_peg_in_depositor_status(
    graph_json: *const c_char,
    esplora_url: *const c_char,
) -> FfiResult {
    FfiResult::err("Not yet implemented")
}

/// Serialize a peg-in graph to JSON
/// 
/// # Arguments  
/// * `graph_json` - JSON-encoded PegInGraph
/// 
/// # Returns
/// FfiResult with JSON string (for saving/sharing)
#[no_mangle]
pub extern "C" fn bridge_serialize_peg_in_graph(
    graph_json: *const c_char,
) -> FfiResult {
    // Just validate and return
    if graph_json.is_null() {
        return FfiResult::err("Null graph pointer");
    }

    let graph_str = unsafe {
        match CStr::from_ptr(graph_json).to_str() {
            Ok(s) => s,
            Err(_) => return FfiResult::err("Invalid UTF-8"),
        }
    };

    // Validate it's valid JSON
    match serde_json::from_str::<serde_json::Value>(graph_str) {
        Ok(_) => FfiResult::ok(graph_str.to_string().into_bytes()),
        Err(e) => FfiResult::err(&format!("Invalid JSON: {}", e)),
    }
}

/// Deserialize a peg-in graph from JSON
/// 
/// # Arguments
/// * `json_data` - JSON-encoded graph data
/// 
/// # Returns
/// FfiResult with validated JSON
#[no_mangle]
pub extern "C" fn bridge_deserialize_peg_in_graph(
    json_data: *const c_char,
) -> FfiResult {
    bridge_serialize_peg_in_graph(json_data)
}
