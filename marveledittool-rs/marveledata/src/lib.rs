//! Binary data model helpers for Marvel vs. Capcom 3 table files.

mod io;
pub mod types;

pub use io::{load_first, save_first};

use thiserror::Error;

/// Unified error type for all marveledata operations.
#[derive(Debug, Error)]
pub enum Error {
    /// Wrapper around std::io failures.
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),
    /// Wrapper around binrw parsing/writing failures.
    #[error("binary parsing error: {0}")]
    Binrw(#[from] binrw::Error),
    /// Encountered a pointer table shorter than the header declared.
    #[error("pointer table truncated: expected {expected} bytes, read {actual}")]
    TruncatedPointerTable { expected: u64, actual: u64 },
    /// Pointer table offsets must point past the pointer table itself.
    #[error("pointer offset {offset:#x} overlaps the pointer table ending at {table_end:#x}")]
    OffsetBeforeData { offset: u64, table_end: u64 },
    /// Offsets must live within the file bounds.
    #[error("pointer offset {offset:#x} exceeds file length {file_len:#x}")]
    OffsetOutOfBounds { offset: u64, file_len: u64 },
    /// All non-zero offsets require a matching length calculation.
    #[error("no payload length could be derived for offset {offset:#x}")]
    MissingLengthForOffset { offset: u64 },
    /// Payload lengths must match the gap to the next pointer.
    #[error("entry {index:#x} expected {expected} bytes, found {actual}")]
    PayloadLengthMismatch {
        index: u32,
        expected: u64,
        actual: u64,
    },
    /// Entries with a zero offset represent empty table slots.
    #[error("entry {index:#x} has data but zero offset")]
    NonEmptyZeroOffset { index: u32 },
}
