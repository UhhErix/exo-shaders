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
