# Living-space onboarding changes

- Keep existing q01 and q02–q34 IDs and reward definitions. Insert q01_bedroom and q01_dining before q02. Previously claimed quests retain their status.
- Scan current asteroid inventory and sanitation construction/reachability separately from supply readiness. Unknown inventory is not interpreted as zero dirt.
- Recognize native Barracks and MessHall results; do not reproduce room-size rules. Working, reachable facilities are checked separately from room recognition.
- Record Learned per colony (registry v5), migrate Claimed history, and credit the three living-space quests when real conditions were already satisfied before acceptance. Rewards and forward quest access retain the existing claim workflow.
- After facility failure, retain Learned and show repair guidance. Current objective checks still gate reward claiming. Keep original tutorial atlas, pause/replay and real toilet-then-wash witness; demo playback cannot complete a quest.
- Fix an existing out-of-scope objective reference in the reward row layout.

Validation: 20 core checks pass. Full mod build and in-game verification are pending because game Managed DLLs are not available here. New Cot/MessTable Workable/Operational and native RoomTypes member observations must be verified against the installed game version; unsupported observations fail closed rather than grant progress.

The private web console is a separate simulation interface with matching task IDs. No telemetry network bridge is added to the mod in this change.

## Task shortcuts and duplicant monitoring

- Completion and claim notifications now carry a quest link bound to the active colony. Clicking opens the exact quest and expands its phase. Links from another colony or removed quests are ignored. Actionable notices are not consolidated across different tasks.
- The existing management button toggles the task window. A second task shortcut remains visible on the compact HUD, including while it is folded. The Details button opens monitoring in the task window.
- A shared, session-only monitor observes live duplicants on the current asteroid: health %, stress %, breath % and calories (kcal). Both views share selection and use previous/next controls. No values are written back to game state or quest history.
- Native roster add/remove events maintain the registry. A once-per-second coroutine samples only registered duplicants; there is no scene/Grid/building scan for monitoring. Event subscriptions, coroutine, HUD and task window are released when leaving the game.
- Native CrewPortrait prefabs are reused where available, bound to the duplicant's assignables proxy. If native UI assets are not ready, initials are shown and binding is retried. Unknown/non-applicable values remain explicit, including calories on bionic duplicants.

Validation: 38 core checks pass (previous 20 plus 18 monitor and notification-link checks). Full build and in-game UI/portrait/callback verification still require the installed game Managed DLLs. This is source in a draft PR, not a tested game binary. DLC/start-world detection and chapter artwork are deferred; no additional plugins or Miro board are needed.
