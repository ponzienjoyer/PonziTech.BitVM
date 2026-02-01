use crate::FfiResult;
use std::os::raw::c_char;

#[cfg(not(windows))]
use bitcoin::{Amount, Network, OutPoint, PublicKey, Txid};
#[cfg(not(windows))]
use bridge::client::esplora::get_esplora_url;
#[cfg(not(windows))]
use bridge::contexts::{
    depositor::DepositorContext, operator::OperatorContext, verifier::VerifierContext,
};
#[cfg(not(windows))]
use bridge::graphs::base::BaseGraph;
#[cfg(not(windows))]
use bridge::graphs::peg_in::{PegInDepositorStatus, PegInGraph};
#[cfg(not(windows))]
use bridge::serialization::try_deserialize;
#[cfg(not(windows))]
use bridge::transactions::base::Input;
#[cfg(not(windows))]
use esplora_client::Builder as EsploraBuilder;
#[cfg(not(windows))]
use serde::{Deserialize, Serialize};
#[cfg(not(windows))]
use std::ffi::CStr;
#[cfg(not(windows))]
use std::str::FromStr;
#[cfg(not(windows))]
use std::sync::OnceLock;
#[cfg(not(windows))]
use tokio::runtime::Runtime;

#[cfg(windows)]
const BRIDGE_UNSUPPORTED: &str =
    "Bridge FFI is not available on Windows (upstream depends on unix-only openssh/sftp)";

#[cfg(windows)]
fn unsupported() -> FfiResult {
    FfiResult::err(BRIDGE_UNSUPPORTED)
}

#[cfg(not(windows))]
static RUNTIME: OnceLock<Runtime> = OnceLock::new();

#[cfg(not(windows))]
fn runtime() -> &'static Runtime {
    RUNTIME.get_or_init(|| Runtime::new().expect("Failed to initialize tokio runtime"))
}

#[cfg(not(windows))]
#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DepositorContextDto {
    network: String,
    depositor_secret: String,
    verifier_public_keys: Vec<String>,
}

#[cfg(not(windows))]
#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct OperatorContextDto {
    network: String,
    operator_secret: String,
    verifier_public_keys: Vec<String>,
}

#[cfg(not(windows))]
#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct VerifierContextDto {
    network: String,
    verifier_secret: String,
    verifier_public_keys: Vec<String>,
}

#[cfg(not(windows))]
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct PegInStatusDto {
    graph_id: String,
    code: String,
    message: String,
}

#[cfg(not(windows))]
fn read_cstr(ptr: *const c_char, label: &str) -> Result<String, String> {
    if ptr.is_null() {
        return Err(format!("Null pointer for {label}"));
    }

    let cstr = unsafe { CStr::from_ptr(ptr) };
    cstr.to_str()
        .map(|s| s.to_string())
        .map_err(|_| format!("Invalid UTF-8 in {label}"))
}

#[cfg(not(windows))]
fn parse_network(network_str: &str) -> Result<Network, String> {
    match network_str.to_lowercase().as_str() {
        "mainnet" | "bitcoin" => Ok(Network::Bitcoin),
        "testnet" => Ok(Network::Testnet),
        "signet" => Ok(Network::Signet),
        "regtest" => Ok(Network::Regtest),
        _ => Err("Invalid network".to_string()),
    }
}

#[cfg(not(windows))]
fn parse_public_keys(keys: &[String]) -> Result<Vec<PublicKey>, String> {
    keys.iter()
        .map(|key| {
            let bytes = hex::decode(key)
                .map_err(|_| format!("Invalid public key hex: {key}"))?;
            PublicKey::from_slice(&bytes)
                .map_err(|_| format!("Invalid public key bytes: {key}"))
        })
        .collect()
}

#[cfg(not(windows))]
fn serialize_json<T: Serialize>(value: &T) -> FfiResult {
    match serde_json::to_vec(value) {
        Ok(bytes) => FfiResult::ok(bytes),
        Err(e) => FfiResult::err(&format!("JSON serialization error: {e}")),
    }
}

#[cfg(not(windows))]
fn validate_context<F>(factory: F) -> Result<(), String>
where
    F: FnOnce() + std::panic::UnwindSafe,
{
    std::panic::catch_unwind(factory)
        .map_err(|_| "Invalid secret or public keys".to_string())
}

#[cfg(not(windows))]
fn status_code(status: &PegInDepositorStatus) -> &'static str {
    match status {
        PegInDepositorStatus::PegInDepositWait => "PegInDepositWait",
        PegInDepositorStatus::PegInConfirmWait => "PegInConfirmWait",
        PegInDepositorStatus::PegInConfirmComplete => "PegInConfirmComplete",
        PegInDepositorStatus::PegInRefundAvailable => "PegInRefundAvailable",
        PegInDepositorStatus::PegInRefundComplete => "PegInRefundComplete",
    }
}

/// Create a depositor context
///
/// # Arguments
/// * `network` - Network name ("mainnet", "testnet", "signet", "regtest")
/// * `depositor_secret` - Depositor secret key (hex)
/// * `verifier_public_keys_json` - JSON array of verifier public keys (hex)
///
/// # Returns
/// FfiResult with JSON-encoded context on success
#[no_mangle]
pub extern "C" fn bridge_create_depositor_context(
    network: *const c_char,
    depositor_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    bridge_create_depositor_context_impl(network, depositor_secret, verifier_public_keys_json)
}

#[cfg(not(windows))]
fn bridge_create_depositor_context_impl(
    network: *const c_char,
    depositor_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    let network_str = match read_cstr(network, "network") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let secret_str = match read_cstr(depositor_secret, "depositor_secret") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let verifier_keys_str = match read_cstr(verifier_public_keys_json, "verifier_public_keys") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };

    let network = match parse_network(&network_str) {
        Ok(n) => n,
        Err(e) => return FfiResult::err(&e),
    };
    let verifier_keys: Vec<String> = match serde_json::from_str(&verifier_keys_str) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&format!("JSON parse error: {e}")),
    };
    let verifier_public_keys = match parse_public_keys(&verifier_keys) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&e),
    };

    if let Err(e) = validate_context(|| {
        let _ = DepositorContext::new(network, &secret_str, &verifier_public_keys);
    }) {
        return FfiResult::err(&e);
    }

    let dto = DepositorContextDto {
        network: network_str,
        depositor_secret: secret_str,
        verifier_public_keys: verifier_keys,
    };

    serialize_json(&dto)
}

#[cfg(windows)]
fn bridge_create_depositor_context_impl(
    _network: *const c_char,
    _depositor_secret: *const c_char,
    _verifier_public_keys_json: *const c_char,
) -> FfiResult {
    unsupported()
}

/// Create an operator context
///
/// # Arguments
/// * `network` - Network name
/// * `operator_secret` - Operator secret key (hex)
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
    bridge_create_operator_context_impl(network, operator_secret, verifier_public_keys_json)
}

#[cfg(not(windows))]
fn bridge_create_operator_context_impl(
    network: *const c_char,
    operator_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    let network_str = match read_cstr(network, "network") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let secret_str = match read_cstr(operator_secret, "operator_secret") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let verifier_keys_str = match read_cstr(verifier_public_keys_json, "verifier_public_keys") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };

    let network = match parse_network(&network_str) {
        Ok(n) => n,
        Err(e) => return FfiResult::err(&e),
    };
    let verifier_keys: Vec<String> = match serde_json::from_str(&verifier_keys_str) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&format!("JSON parse error: {e}")),
    };
    let verifier_public_keys = match parse_public_keys(&verifier_keys) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&e),
    };

    if let Err(e) = validate_context(|| {
        let _ = OperatorContext::new(network, &secret_str, &verifier_public_keys);
    }) {
        return FfiResult::err(&e);
    }

    let dto = OperatorContextDto {
        network: network_str,
        operator_secret: secret_str,
        verifier_public_keys: verifier_keys,
    };

    serialize_json(&dto)
}

#[cfg(windows)]
fn bridge_create_operator_context_impl(
    _network: *const c_char,
    _operator_secret: *const c_char,
    _verifier_public_keys_json: *const c_char,
) -> FfiResult {
    unsupported()
}

/// Create a verifier context
///
/// # Arguments
/// * `network` - Network name
/// * `verifier_secret` - Verifier secret key (hex)
/// * `verifier_public_keys_json` - JSON array of verifier public keys (including this one)
///
/// # Returns
/// FfiResult with JSON-encoded context on success
#[no_mangle]
pub extern "C" fn bridge_create_verifier_context(
    network: *const c_char,
    verifier_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    bridge_create_verifier_context_impl(network, verifier_secret, verifier_public_keys_json)
}

#[cfg(not(windows))]
fn bridge_create_verifier_context_impl(
    network: *const c_char,
    verifier_secret: *const c_char,
    verifier_public_keys_json: *const c_char,
) -> FfiResult {
    let network_str = match read_cstr(network, "network") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let secret_str = match read_cstr(verifier_secret, "verifier_secret") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let verifier_keys_str = match read_cstr(verifier_public_keys_json, "verifier_public_keys") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };

    let network = match parse_network(&network_str) {
        Ok(n) => n,
        Err(e) => return FfiResult::err(&e),
    };
    let verifier_keys: Vec<String> = match serde_json::from_str(&verifier_keys_str) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&format!("JSON parse error: {e}")),
    };
    let verifier_public_keys = match parse_public_keys(&verifier_keys) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&e),
    };

    if let Err(e) = validate_context(|| {
        let _ = VerifierContext::new(network, &secret_str, &verifier_public_keys);
    }) {
        return FfiResult::err(&e);
    }

    let dto = VerifierContextDto {
        network: network_str,
        verifier_secret: secret_str,
        verifier_public_keys: verifier_keys,
    };

    serialize_json(&dto)
}

#[cfg(windows)]
fn bridge_create_verifier_context_impl(
    _network: *const c_char,
    _verifier_secret: *const c_char,
    _verifier_public_keys_json: *const c_char,
) -> FfiResult {
    unsupported()
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
    bridge_create_peg_in_graph_impl(
        context_json,
        deposit_txid,
        deposit_vout,
        deposit_amount,
        evm_address,
    )
}

#[cfg(not(windows))]
fn bridge_create_peg_in_graph_impl(
    context_json: *const c_char,
    deposit_txid: *const c_char,
    deposit_vout: u32,
    deposit_amount: u64,
    evm_address: *const c_char,
) -> FfiResult {
    if deposit_amount == 0 {
        return FfiResult::err("Deposit amount must be greater than zero");
    }
    let context_str = match read_cstr(context_json, "context_json") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let deposit_txid_str = match read_cstr(deposit_txid, "deposit_txid") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };
    let evm_address_str = match read_cstr(evm_address, "evm_address") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };

    let context: DepositorContextDto = match serde_json::from_str(&context_str) {
        Ok(dto) => dto,
        Err(e) => return FfiResult::err(&format!("Context JSON parse error: {e}")),
    };
    let network = match parse_network(&context.network) {
        Ok(n) => n,
        Err(e) => return FfiResult::err(&e),
    };
    let verifier_public_keys = match parse_public_keys(&context.verifier_public_keys) {
        Ok(keys) => keys,
        Err(e) => return FfiResult::err(&e),
    };

    let depositor_context = match std::panic::catch_unwind(|| {
        DepositorContext::new(network, &context.depositor_secret, &verifier_public_keys)
    }) {
        Ok(ctx) => ctx,
        Err(_) => return FfiResult::err("Invalid depositor secret or verifier public keys"),
    };

    let txid = match Txid::from_str(&deposit_txid_str) {
        Ok(txid) => txid,
        Err(_) => return FfiResult::err("Invalid deposit txid"),
    };

    let input = Input {
        outpoint: OutPoint { txid, vout: deposit_vout },
        amount: Amount::from_sat(deposit_amount),
    };

    let graph = PegInGraph::new(&depositor_context, input, &evm_address_str);

    match serde_json::to_vec(&graph) {
        Ok(bytes) => FfiResult::ok(bytes),
        Err(e) => FfiResult::err(&format!("JSON serialization error: {e}")),
    }
}

#[cfg(windows)]
fn bridge_create_peg_in_graph_impl(
    _context_json: *const c_char,
    _deposit_txid: *const c_char,
    _deposit_vout: u32,
    _deposit_amount: u64,
    _evm_address: *const c_char,
) -> FfiResult {
    unsupported()
}

/// Get peg-in graph status for depositor
///
/// # Arguments
/// * `graph_json` - JSON-encoded PegInGraph
/// * `esplora_url` - Esplora API URL (optional)
///
/// # Returns
/// FfiResult with JSON-encoded status
#[no_mangle]
pub extern "C" fn bridge_get_peg_in_depositor_status(
    graph_json: *const c_char,
    esplora_url: *const c_char,
) -> FfiResult {
    bridge_get_peg_in_depositor_status_impl(graph_json, esplora_url)
}

#[cfg(not(windows))]
fn bridge_get_peg_in_depositor_status_impl(
    graph_json: *const c_char,
    esplora_url: *const c_char,
) -> FfiResult {
    let graph_str = match read_cstr(graph_json, "graph_json") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };

    let graph: PegInGraph = match try_deserialize(&graph_str) {
        Ok(graph) => graph,
        Err(e) => return FfiResult::err(&e),
    };

    let esplora_url_str = if esplora_url.is_null() {
        get_esplora_url(graph.network()).to_string()
    } else {
        match read_cstr(esplora_url, "esplora_url") {
            Ok(s) if s.trim().is_empty() => get_esplora_url(graph.network()).to_string(),
            Ok(s) => s,
            Err(e) => return FfiResult::err(&e),
        }
    };

    let client = match EsploraBuilder::new(&esplora_url_str).build_async() {
        Ok(client) => client,
        Err(e) => return FfiResult::err(&format!("Failed to build esplora client: {e}")),
    };

    let status = runtime().block_on(async { graph.depositor_status(&client).await });

    let dto = PegInStatusDto {
        graph_id: graph.id().clone(),
        code: status_code(&status).to_string(),
        message: status.to_string(),
    };

    serialize_json(&dto)
}

#[cfg(windows)]
fn bridge_get_peg_in_depositor_status_impl(
    _graph_json: *const c_char,
    _esplora_url: *const c_char,
) -> FfiResult {
    unsupported()
}

/// Serialize a peg-in graph to JSON
///
/// # Arguments
/// * `graph_json` - JSON-encoded PegInGraph
///
/// # Returns
/// FfiResult with normalized JSON string
#[no_mangle]
pub extern "C" fn bridge_serialize_peg_in_graph(graph_json: *const c_char) -> FfiResult {
    bridge_serialize_peg_in_graph_impl(graph_json)
}

#[cfg(not(windows))]
fn bridge_serialize_peg_in_graph_impl(graph_json: *const c_char) -> FfiResult {
    let graph_str = match read_cstr(graph_json, "graph_json") {
        Ok(s) => s,
        Err(e) => return FfiResult::err(&e),
    };

    let graph: PegInGraph = match try_deserialize(&graph_str) {
        Ok(graph) => graph,
        Err(e) => return FfiResult::err(&e),
    };

    match serde_json::to_vec(&graph) {
        Ok(bytes) => FfiResult::ok(bytes),
        Err(e) => FfiResult::err(&format!("JSON serialization error: {e}")),
    }
}

#[cfg(windows)]
fn bridge_serialize_peg_in_graph_impl(_graph_json: *const c_char) -> FfiResult {
    unsupported()
}

/// Deserialize a peg-in graph from JSON
///
/// # Arguments
/// * `json_data` - JSON-encoded graph data
///
/// # Returns
/// FfiResult with validated JSON
#[no_mangle]
pub extern "C" fn bridge_deserialize_peg_in_graph(json_data: *const c_char) -> FfiResult {
    bridge_deserialize_peg_in_graph_impl(json_data)
}

#[cfg(not(windows))]
fn bridge_deserialize_peg_in_graph_impl(json_data: *const c_char) -> FfiResult {
    bridge_serialize_peg_in_graph_impl(json_data)
}

#[cfg(windows)]
fn bridge_deserialize_peg_in_graph_impl(_json_data: *const c_char) -> FfiResult {
    unsupported()
}
