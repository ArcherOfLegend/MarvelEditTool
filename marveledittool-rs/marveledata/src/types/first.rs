use binrw::binrw;
use serde::{Deserialize, Serialize};

/// Pointer table header found in `MarvelData/TableFile.cs` (`LoadFile`) with a size of 0x10 bytes.
#[binrw]
#[brw(little)]
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct FirstHeader {
    /// 0x08 bytes copied from the unmanaged header block (`TableFile.header`).
    pub header_a: [u8; 8],
    /// 0x04 bytes: entry count written by `TableFile.LoadFile`.
    pub entry_count: u32,
    /// 0x04 bytes copied from `TableFile.headerB`.
    pub header_b: [u8; 4],
}

impl FirstHeader {
    pub const SIZE: usize = 8 + 4 + 4;

    #[must_use]
    pub fn entry_count(&self) -> usize {
        self.entry_count as usize
    }

    #[must_use]
    pub fn pointer_table_len(&self) -> usize {
        Self::SIZE + self.entry_count() * FirstEntryRecord::SIZE
    }
}

/// Individual pointer table record written in `MarvelData/TableFile.cs` (`TableEntry` loop) with a size of 0x08 bytes.
#[binrw]
#[brw(little)]
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct FirstEntryRecord {
    /// 0x04 bytes: logical entry index from `TableEntry.index`.
    pub index: u32,
    /// 0x04 bytes: absolute offset into the file stored in `TableEntry.originalPointer`.
    pub offset: u32,
}

impl FirstEntryRecord {
    pub const SIZE: usize = 4 + 4;
}

/// Logical entry that combines pointer table metadata with the raw payload bytes.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct FirstEntry {
    /// 0x04 bytes: logical entry index from the pointer table.
    pub index: u32,
    /// 0x04 bytes: absolute offset from the pointer table.
    pub offset: u32,
    /// Raw payload block delimited by successive offsets.
    pub data: Vec<u8>,
}

/// In-memory representation of the simplified table format handled in this task.
#[derive(Clone, Debug, PartialEq, Eq, Serialize, Deserialize)]
pub struct Model {
    /// 0x10 bytes: table header as defined in `TableFile.cs`.
    pub header: FirstHeader,
    /// Sequence of pointer table rows paired with their raw payload data.
    pub entries: Vec<FirstEntry>,
}
