# Core checks

Run with .NET 8 SDK: `dotnet run --project Tests/CoreTests.csproj -- quests.json` from repository root.

The harness compiles the actual pure evaluators, guidance selector, playback state, quest models and QuestStore. Only logging, language selection and the game scanner/witness are stubs. It checks sanitation chain connectivity, world isolation, native room-result separation, unknown inventory, repair guidance, real trial state, per-save history and v4 migration.

These checks do not validate Unity, native building components, room type members, Harmony hooks or UI rendering. Build Rookie100 against the installed game's Managed DLLs and test in-game before release. No Steam release or main-branch merge was performed.

The monitoring extension adds 18 checks for world filtering, copied observations, shared selection and removal, colony switches, unknown/native amount handling, and stale/removed notification links. MonitorState and QuestNoticeLink are the actual production implementations; Unity rendering and native roster events remain outside this harness.
