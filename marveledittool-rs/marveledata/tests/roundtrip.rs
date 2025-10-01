use std::fs;
use std::path::PathBuf;

#[test]
fn sample_file_is_non_empty() {
    let mut path = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    path.push("tests/data/sample.bin");
    let bytes = fs::read(&path).expect("sample file should be readable");
    assert!(bytes.len() > 0, "sample fixture must not be empty");
}
