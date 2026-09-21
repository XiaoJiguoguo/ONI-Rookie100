using System.Collections.Generic;

namespace Rookie100.Diagnostics
{
    internal enum DiffuserFinding
    {
        Running, NoWire, NoPower, NoInput, OtherConditions, Idle, Incomplete,
        Overpressure, ManualDisabled, AutomationDisabled, Broken
    }

    // Pure rules: no Unity objects, mutations, persistence or quest state.
    internal static class DiffuserRules
    {
        internal static bool IsRunning(bool? active, bool? operational, bool? hasWire, bool? powered)
        {
            return active == true && operational == true && hasWire == true && powered == true;
        }

        internal static IReadOnlyList<DiffuserFinding> Evaluate(
            bool? active, bool? operational, bool? hasWire, bool? powered, bool? hasInput,
            bool? overpressure, bool? manualDisabled, bool? automationDisabled, bool? broken)
        {
            var findings = new List<DiffuserFinding>();
            if (manualDisabled == true) findings.Add(DiffuserFinding.ManualDisabled);
            if (automationDisabled == true) findings.Add(DiffuserFinding.AutomationDisabled);
            if (broken == true) findings.Add(DiffuserFinding.Broken);
            if (overpressure == true) findings.Add(DiffuserFinding.Overpressure);

            if (hasWire == false) findings.Add(DiffuserFinding.NoWire);
            else if (hasWire == true && powered == false) findings.Add(DiffuserFinding.NoPower);
            // A running converter may temporarily have less than its start threshold.
            bool running = findings.Count == 0 && IsRunning(active, operational, hasWire, powered);
            if (running) findings.Add(DiffuserFinding.Running);
            else if (hasInput == false) findings.Add(DiffuserFinding.NoInput);

            if (!active.HasValue || !operational.HasValue || !hasWire.HasValue ||
                !powered.HasValue || !hasInput.HasValue || !overpressure.HasValue ||
                !manualDisabled.HasValue || !automationDisabled.HasValue || !broken.HasValue)
                findings.Add(DiffuserFinding.Incomplete);

            if (findings.Count == 0)
                findings.Add(operational == false ? DiffuserFinding.OtherConditions : DiffuserFinding.Idle);
            return findings.AsReadOnly();
        }
    }
}
