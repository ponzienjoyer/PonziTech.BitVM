use std::env;
use std::path::Path;

fn main() {
    let crate_dir = env::var("CARGO_MANIFEST_DIR").unwrap();
    let out_dir = env::var("OUT_DIR").unwrap();

    // Generate C# bindings using csbindgen
    csbindgen::Builder::default()
        .input_extern_file(Path::new(&crate_dir).join("src/lib.rs"))
        .input_extern_file(Path::new(&crate_dir).join("src/core.rs"))
        .input_extern_file(Path::new(&crate_dir).join("src/crypto.rs"))
        .csharp_dll_name("bitvm_ffi")
        .csharp_namespace("PonziTech.BitVM.Native")
        .csharp_class_name("BitVMNative")
        .csharp_class_accessibility("public")
        .generate_to_file(
            Path::new(&out_dir).join("ffi_generated.rs"),
            Path::new(&crate_dir).join("../src/PonziTech.BitVM.Native/NativeMethods.cs"),
        )
        .expect("Failed to generate C# bindings");

    println!("cargo:rerun-if-changed=src/lib.rs");
    println!("cargo:rerun-if-changed=src/core.rs");
    println!("cargo:rerun-if-changed=src/crypto.rs");
}
