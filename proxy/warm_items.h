// scskiller_items.bin in the warm's staging folder: the process's D3D12 item states in a mapped file, so they outlive a
// process that dies without a last line (a fault outside our handlers, a fail-fast). The parent (warm.cpp) then plans the
// retry the process couldn't print.
#pragma once
#include <cstdint>

constexpr wchar_t kItemsFile[] = L"scskiller_items.bin";
// A memory restart's item states for the next process: u64 from, u64 n12, then the states as in the items file.
constexpr wchar_t kCarryFile[] = L"scskiller_carry.bin";
constexpr uint64_t kItemsMagic = 0x31534D4554494B53;  // "SKITEMS1"
struct ItemsHeader {
    uint64_t magic, pid, first, n12, total;  // pid: the child's, so no earlier child's file is taken; the states of items [0, n12) follow, one byte each
};
// 0 not taken, 1 created, 2 failed, 3 unfinished (left for the retry), 4 not created (in flight at a removal or fault),
// 5 skipped, 6 in flight
constexpr uint8_t kItemInFlight = 6;
inline bool item_finished(uint8_t s) { return s == 1 || s == 2 || s == 5; }

// The header of a journal's bytes, when they are that child's journal (a torn or earlier one is not).
inline bool journal_check(const uint8_t* b, uint64_t size, uint64_t pid, ItemsHeader& h) {
    if (size < sizeof(ItemsHeader)) return false;
    for (size_t i = 0; i < sizeof h; ++i) reinterpret_cast<uint8_t*>(&h)[i] = b[i];
    return h.magic == kItemsMagic && h.pid == pid && size == sizeof h + h.n12 && h.first <= h.n12;
}

// The first item a journal's bytes left unfinished, for the child with this pid. The parent reads a child's journal this way
// after ending it, to go on from there.
inline bool journal_first_unfinished(const uint8_t* b, uint64_t size, uint64_t pid, uint64_t& from) {
    ItemsHeader h;
    if (!journal_check(b, size, pid, h)) return false;
    uint64_t j = h.first;
    while (j < h.n12 && item_finished(b[sizeof h + j])) ++j;
    from = j;
    return true;
}

// The items the journal's bytes show finished (created, failed or skipped): the start watchdog's progress after the start.
inline bool journal_finished_count(const uint8_t* b, uint64_t size, uint64_t pid, uint64_t& count) {
    ItemsHeader h;
    if (!journal_check(b, size, pid, h)) return false;
    uint64_t c = 0;
    for (uint64_t j = 0; j < h.n12; ++j) c += item_finished(b[sizeof h + j]);
    count = c;
    return true;
}
