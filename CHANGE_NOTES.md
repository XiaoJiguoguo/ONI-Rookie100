# Living-space onboarding changes

- Keep existing q01 and q02–q34 IDs and reward definitions. Insert q01_bedroom and q01_dining before q02. Previously claimed quests retain their status.
- Scan current asteroid inventory and sanitation construction/reachability separately from supply readiness. Unknown inventory is not interpreted as zero dirt.
- Recognize native Barracks and MessHall results; do not reproduce room-size rules. Working, reachable facilities are checked separately from room recognition.
- Record Learned per colony (registry v5), migrate Claimed history, and credit the three living-space quests when real conditions were already satisfied before acceptance. Rewards and forward quest access retain the existing claim workflow.
- After facility failure, retain Learned and show repair guidance. Current objective checks still gate reward claiming. Keep original tutorial atlas, pause/replay and real toilet-then-wash witness; demo playback cannot complete a quest.
- Fix an existing out-of-scope objective reference in the reward row layout.

Validation: 20 core checks pass. Full mod build and in-game verification are pending because game Managed DLLs are not available here. New Cot/MessTable Workable/Operational and native RoomTypes member observations must be verified against the installed game version; unsupported observations fail closed rather than grant progress.

The private web console is a separate simulation interface with matching task IDs. No telemetry network bridge is added to the mod in this change.
