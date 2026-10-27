using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Import;
using FluentAssertions;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class SettingsImportPlannerTests
{
    [TestMethod]
    public void Plan_Merge_AddsNewServersAndKeepsTheirIds()
    {
        var imported = SettingsSamples.Connection("web");
        var document = SettingsSamples.Document(imported);
        var options = SettingsSamples.Options(ImportMode.Merge);

        SavedConnection[] saved = [];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var step = plan.Steps.Should().ContainSingle().Subject;
        step.Action.Should().Be(ConnectionImportAction.Add);
        step.LocalId.Should().Be(imported.Id);
        plan.Removals.Should().BeEmpty();
    }

    [TestMethod]
    public void Plan_Merge_AnIdenticalServer_IsUnchanged()
    {
        var existing = SettingsSamples.Saved("web");
        var imported = SettingsSamples.Connection("web");
        var document = SettingsSamples.Document(imported);
        var options = SettingsSamples.Options(ImportMode.Merge);

        SavedConnection[] saved = [existing];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var step = plan.Steps.Should().ContainSingle().Subject;
        step.Action.Should().Be(ConnectionImportAction.Unchanged);
        step.LocalId.Should().Be(existing.Id);
    }

    [TestMethod]
    public void Plan_Merge_ADifferentServerWithTheSameName_IsAConflictThatKeepsMineByDefault()
    {
        var existing = SettingsSamples.Saved("web");
        var imported = SettingsSamples.Connection("WEB") with { Host = "10.0.0.5", Port = 2222 };
        var document = SettingsSamples.Document(imported);
        var options = SettingsSamples.Options(ImportMode.Merge);

        SavedConnection[] saved = [existing];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var step = plan.Steps.Should().ContainSingle().Subject;
        step.Action.Should().Be(ConnectionImportAction.Conflict);
        step.Resolution.Should().Be(ConnectionConflictResolution.KeepExisting);
        step.WritesConnection.Should().BeFalse();
        step.Changes.Should().BeEquivalentTo("name", "host", "port");
    }

    [TestMethod]
    public void Plan_Merge_UseImported_UpdatesTheExistingServerInPlace()
    {
        var existing = SettingsSamples.Saved("web");
        var imported = SettingsSamples.Connection("web") with { Host = "10.0.0.5" };
        var document = SettingsSamples.Document(imported);
        var resolutions = new Dictionary<Guid, ConnectionConflictResolution> { [imported.Id] = ConnectionConflictResolution.UseImported };
        var options = SettingsSamples.Options(ImportMode.Merge, resolutions);

        SavedConnection[] saved = [existing];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var step = plan.Steps.Single();
        step.WritesConnection.Should().BeTrue();
        step.IsNewConnection.Should().BeFalse();
        step.LocalId.Should().Be(existing.Id);
        step.Draft.Host.Should().Be("10.0.0.5");
    }

    [TestMethod]
    public void Plan_Merge_KeepBoth_AddsACopyWithAFreeName()
    {
        var existing = SettingsSamples.Saved("web");
        var takenCopy = SettingsSamples.Saved("web (imported)");
        var imported = SettingsSamples.Connection("web") with { Host = "10.0.0.5" };
        var document = SettingsSamples.Document(imported);
        var resolutions = new Dictionary<Guid, ConnectionConflictResolution> { [imported.Id] = ConnectionConflictResolution.KeepBoth };
        var options = SettingsSamples.Options(ImportMode.Merge, resolutions);

        SavedConnection[] saved = [existing, takenCopy];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var step = plan.Steps.Single();
        step.IsNewConnection.Should().BeTrue();
        step.Draft.Name.Should().Be("web (imported 2)");
        step.LocalId.Should().NotBe(existing.Id).And.NotBe(takenCopy.Id);
    }

    [TestMethod]
    public void Plan_Merge_KeepsServersMissingFromTheFile()
    {
        var existing = SettingsSamples.Saved("db");
        var imported = SettingsSamples.Connection("web");
        var document = SettingsSamples.Document(imported);
        var options = SettingsSamples.Options(ImportMode.Merge);

        SavedConnection[] saved = [existing];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        plan.Removals.Should().BeEmpty();
        plan.FinalConnectionNames.Values.Should().BeEquivalentTo("db", "web");
    }

    [TestMethod]
    public void Plan_Replace_UpdatesMatchesAndRemovesServersMissingFromTheFile()
    {
        var kept = SettingsSamples.Saved("web");
        var removed = SettingsSamples.Saved("db");
        var imported = SettingsSamples.Connection("web") with { Port = 2200 };
        var document = SettingsSamples.Document(imported);
        var options = SettingsSamples.Options(ImportMode.Replace);

        SavedConnection[] saved = [kept, removed];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        plan.Steps.Single().Action.Should().Be(ConnectionImportAction.Update);
        plan.Removals.Should().ContainSingle().Which.Id.Should().Be(removed.Id);
    }

    [TestMethod]
    public void Plan_AnImportedIdThatIsAlreadyUsedByAnotherServer_GetsANewId()
    {
        var existing = SettingsSamples.Saved("renamed-locally");
        var imported = SettingsSamples.Connection("web", existing.Id);
        var document = SettingsSamples.Document(imported);
        var options = SettingsSamples.Options(ImportMode.Merge);

        SavedConnection[] saved = [existing];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var step = plan.Steps.Single();
        step.Action.Should().Be(ConnectionImportAction.Add);
        step.LocalId.Should().NotBe(existing.Id);
        plan.IdMap[imported.Id].Should().Be(step.LocalId);
    }

    [TestMethod]
    public void Plan_RemapsJumpHostsToTheLocalServer()
    {
        var existingBastion = SettingsSamples.Saved("bastion");
        var importedBastion = SettingsSamples.Connection("bastion");
        var web = SettingsSamples.Connection("web", jumpHostId: importedBastion.Id);
        var document = SettingsSamples.Document(importedBastion, web);
        var options = SettingsSamples.Options(ImportMode.Merge);

        SavedConnection[] saved = [existingBastion];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var webStep = plan.Steps.Single(step => step.Imported.Id == web.Id);
        webStep.Draft.JumpHostId.Should().Be(existingBastion.Id);
    }

    [TestMethod]
    public void Plan_WithoutTheConnectionsSection_SkipsServersButStillMapsIdsByName()
    {
        var existing = SettingsSamples.Saved("web");
        var imported = SettingsSamples.Connection("web");
        var document = SettingsSamples.Document(imported);
        var sections = new SettingsSections(false, true, true, true);
        var resolutions = new Dictionary<Guid, ConnectionConflictResolution>();
        var options = new SettingsImportOptions(ImportMode.Replace, sections, resolutions);

        SavedConnection[] saved = [existing];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        plan.Steps.Single().Action.Should().Be(ConnectionImportAction.Skipped);
        plan.Removals.Should().BeEmpty();
        plan.MapConnectionId(imported.Id).Should().Be(existing.Id);
    }

    [TestMethod]
    public void Plan_KeepBothForAJumpHost_PointsImportedDependentsToTheCopy()
    {
        var existingBastion = SettingsSamples.Saved("bastion");
        var importedBastion = SettingsSamples.Connection("bastion") with { Port = 2022 };
        var web = SettingsSamples.Connection("web", jumpHostId: importedBastion.Id);
        var document = SettingsSamples.Document(importedBastion, web);
        var resolutions = new Dictionary<Guid, ConnectionConflictResolution> { [importedBastion.Id] = ConnectionConflictResolution.KeepBoth };
        var options = SettingsSamples.Options(ImportMode.Merge, resolutions);

        SavedConnection[] saved = [existingBastion];
        var plan = SettingsImportPlanner.Plan(document, saved, options).Value;

        var copy = plan.Steps.Single(step => step.Imported.Id == importedBastion.Id);
        var webStep = plan.Steps.Single(step => step.Imported.Id == web.Id);
        webStep.Draft.JumpHostId.Should().Be(copy.LocalId);
        plan.FinalConnectionNames.Should().HaveCount(3);
    }
}
