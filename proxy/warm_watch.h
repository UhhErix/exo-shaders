// The start watchdog of the warm's parent (warm.cpp): a child that shows no progress for the start limit is ended and started
// again, from its journal, and only kStartStalls such timeouts in a row fail the warm. Progress is any rise of the child's
// work counter: its CPU time, its I/O bytes, its page faults and the output it relayed. A db step that computes, pages in or
// writes moves one; a hung child moves none.
#pragma once
#include <cstdint>

constexpr int kStartStalls = 3;  // timeouts in a row, with no start between, that fail the warm

struct StartWatch {
    uint64_t last = 0;   // the work counter at the last turn
    bool seen = false;   // false: a new child's first reading is its baseline, not progress
    double quiet = 0;    // ms with no progress since the last progress or timeout
    int stalls = 0;      // timeouts since the child last started
};

enum class StartStep { wait, relaunch, give_up };

// One turn of turn_ms. limit_ms: the progress-free time that times a start out (the caller sizes it from the last db step).
// loading: the child reads the dbs before the device, which the start window never counted. started: the child's started
// event is set, the watch is done.
inline StartStep start_watch_tick(StartWatch& w, uint64_t work, bool started, bool loading, double limit_ms, double turn_ms) {
    if (started) return w.stalls = 0, StartStep::wait;
    if (!w.seen) return w.seen = true, w.last = work, StartStep::wait;
    if (work != w.last) return w.last = work, w.quiet = 0, StartStep::wait;
    if (loading) return StartStep::wait;
    if ((w.quiet += turn_ms) < limit_ms) return StartStep::wait;
    w.quiet = 0;
    return ++w.stalls >= kStartStalls ? StartStep::give_up : StartStep::relaunch;
}

// A relaunched child: its counters start again, and the timeouts before it still count.
inline void start_watch_child(StartWatch& w) { w.seen = false, w.quiet = 0; }

// The hard limits, over the progress rules: a child that spins CPU counts as progress, so these look only at the clock and at
// the finished items. Before the start event: no start within kStartDeadlineMs of the launch. After it: no finished item for
// kItemStallMs. Either one trips the first time to a resume from the journal, and again to a failure: one resume for the warm.
constexpr double kStartDeadlineMs = 30 * 60 * 1000.0;
constexpr double kItemStallMs = 20 * 60 * 1000.0;

struct HardWatch {
    double age = 0;        // ms since the child launched
    double idle = 0;       // ms since the finished count last moved, after the start event
    uint64_t items = 0;    // the finished count at the last check
    bool resumed = false;  // the warm's one resume is used, kept over the children
};

enum class HardStep { ok, resume, fail };

inline void hard_watch_child(HardWatch& h) { h.age = 0, h.idle = 0, h.items = 0; }

// finished: the journal's finished count (read by the caller every few turns). turn_ms: the loop's turn.
inline HardStep hard_watch_tick(HardWatch& h, bool started, uint64_t finished, double turn_ms) {
    h.age += turn_ms;
    bool trip;
    if (!started) trip = h.age >= kStartDeadlineMs;
    else {
        if (finished != h.items) h.items = finished, h.idle = 0;
        else h.idle += turn_ms;
        trip = h.idle >= kItemStallMs;
    }
    if (!trip) return HardStep::ok;
    if (h.resumed) return HardStep::fail;
    return h.resumed = true, HardStep::resume;
}
