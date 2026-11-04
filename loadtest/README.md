# Load tests

* `k6/seed.js` creates the `ds-100x500` dataset (100 users x 500 tasks) through the API.
* `k6/tasks.js` runs the mixed workload and writes `results/<LABEL>.json`.
* `run.sh <label> <image>` repeats reset -> seed -> workload 3 times on the Compose stack.

The method and results are in [../docs/PERFORMANCE.md](../docs/PERFORMANCE.md).
