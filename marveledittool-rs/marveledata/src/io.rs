use std::collections::HashMap;
use std::fs;
use std::io::{Cursor, Seek, SeekFrom, Write};
use std::path::Path;

use binrw::{BinRead, BinWrite};

use crate::types::first::{FirstEntry, FirstEntryRecord, FirstHeader, Model};
use crate::Error;

pub fn load_first(path: impl AsRef<Path>) -> Result<Model, Error> {
    let bytes = fs::read(path)?;
    let mut cursor = Cursor::new(&bytes);

    let header = FirstHeader::read(&mut cursor)?;
    let entry_count = header.entry_count();
    let pointer_table_len = header.pointer_table_len();

    if bytes.len() < pointer_table_len {
        return Err(Error::TruncatedPointerTable {
            expected: pointer_table_len as u64,
            actual: bytes.len() as u64,
        });
    }

    let mut records = Vec::with_capacity(entry_count);
    for _ in 0..entry_count {
        records.push(FirstEntryRecord::read(&mut cursor)?);
    }

    let table_end = cursor.position() as usize;

    let mut offset_to_len = HashMap::new();
    let mut ordered_offsets: Vec<u32> = records
        .iter()
        .filter_map(|record| (record.offset != 0).then_some(record.offset))
        .collect();
    ordered_offsets.sort_unstable();
    ordered_offsets.dedup();

    if let Some(first_offset) = ordered_offsets.first() {
        if (*first_offset as usize) < table_end {
            return Err(Error::OffsetBeforeData {
                offset: *first_offset as u64,
                table_end: table_end as u64,
            });
        }
    }

    for window in ordered_offsets.windows(2) {
        if let [current, next] = window {
            let current_usize = *current as usize;
            let next_usize = *next as usize;
            if next_usize > bytes.len() {
                return Err(Error::OffsetOutOfBounds {
                    offset: *next as u64,
                    file_len: bytes.len() as u64,
                });
            }
            offset_to_len.insert(*current, next_usize - current_usize);
        }
    }

    if let Some(&last_offset) = ordered_offsets.last() {
        let start = last_offset as usize;
        if start > bytes.len() {
            return Err(Error::OffsetOutOfBounds {
                offset: last_offset as u64,
                file_len: bytes.len() as u64,
            });
        }
        offset_to_len.insert(last_offset, bytes.len() - start);
    }

    let mut entries = Vec::with_capacity(records.len());
    for record in records {
        if record.offset == 0 {
            entries.push(FirstEntry {
                index: record.index,
                offset: record.offset,
                data: Vec::new(),
            });
            continue;
        }

        let offset = record.offset as usize;
        let length =
            *offset_to_len
                .get(&record.offset)
                .ok_or_else(|| Error::MissingLengthForOffset {
                    offset: record.offset as u64,
                })?;
        let end = offset + length;
        if end > bytes.len() {
            return Err(Error::OffsetOutOfBounds {
                offset: record.offset as u64 + length as u64,
                file_len: bytes.len() as u64,
            });
        }
        let data = bytes[offset..end].to_vec();
        entries.push(FirstEntry {
            index: record.index,
            offset: record.offset,
            data,
        });
    }

    Ok(Model { header, entries })
}

pub fn save_first(path: impl AsRef<Path>, model: &Model) -> Result<(), Error> {
    let mut header = model.header.clone();
    header.entry_count = model.entries.len() as u32;

    let mut sorted: Vec<&FirstEntry> = model
        .entries
        .iter()
        .filter(|entry| entry.offset != 0)
        .collect();
    sorted.sort_unstable_by_key(|entry| entry.offset);

    let pointer_table_len = header.pointer_table_len();
    if let Some(first) = sorted.first() {
        if (first.offset as usize) < pointer_table_len {
            return Err(Error::OffsetBeforeData {
                offset: first.offset as u64,
                table_end: pointer_table_len as u64,
            });
        }
    }

    for pair in sorted.windows(2) {
        let current = pair[0];
        let next = pair[1];
        let expected = next.offset - current.offset;
        let actual = current.data.len() as u32;
        if expected != actual {
            return Err(Error::PayloadLengthMismatch {
                index: current.index,
                expected: expected as u64,
                actual: actual as u64,
            });
        }
    }

    for entry in &model.entries {
        if entry.offset == 0 && !entry.data.is_empty() {
            return Err(Error::NonEmptyZeroOffset { index: entry.index });
        }
    }

    let final_len = if let Some(last) = sorted.last() {
        last.offset as usize + last.data.len()
    } else {
        pointer_table_len
    };

    let mut buffer = vec![0u8; final_len];
    {
        let mut cursor = Cursor::new(&mut buffer[..]);
        header.write(&mut cursor)?;
        for entry in &model.entries {
            FirstEntryRecord {
                index: entry.index,
                offset: entry.offset,
            }
            .write(&mut cursor)?;
        }

        for entry in sorted {
            cursor.seek(SeekFrom::Start(entry.offset as u64))?;
            cursor.write_all(&entry.data)?;
        }
    }

    fs::write(path, buffer)?;
    Ok(())
}
