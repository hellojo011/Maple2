using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;
using M2dXmlGenerator;
using Maple2.File.IO;
using Maple2.File.IO.Crypto.Common;
using Maple2.File.Parser.Enum;
using Maple2.File.Parser.Tools;
using Maple2.File.Parser.Xml.Quest;
using Maple2.File.Parser.Xml.String;
using Maple2.Model.Enum;
using Maple2.Model.Metadata;
using ConditionType = Maple2.Model.Enum.ConditionType;
using ExpType = Maple2.Model.Enum.ExpType;

namespace Maple2.File.Ingest.Mapper;

public class QuestMapper : TypeMapper<QuestMetadata> {
    private readonly M2dReader xmlReader;
    private readonly string language;
    private readonly XmlSerializer questSerializer = new(typeof(QuestDataRootRoot));
    private readonly XmlSerializer descriptionSerializer = new(typeof(QuestDescriptionRoot));

    public QuestMapper(M2dReader xmlReader, string language) {
        this.xmlReader = xmlReader;
        this.language = language;
    }

    protected override IEnumerable<QuestMetadata> Map() {
        Dictionary<int, string> questNames = ParseQuestNames();
        foreach ((int id, string name, QuestData data) in ParseQuests(questNames)) {
            Debug.Assert(Enum.IsDefined((QuestType) data.basic.questType), $"Invalid QuestType: {data.basic.questType}");
            var unrequiredAchievement = (0, 0);
            if (data.require.unreqAchievement.Length == 2 &&
                int.TryParse(data.require.unreqAchievement[0], out int achievementId) &&
                int.TryParse(data.require.unreqAchievement[1], out int grade)) {
                unrequiredAchievement = (achievementId, grade);
            }
            yield return new QuestMetadata(
                Id: id,
                Name: name,
                Basic: new QuestMetadataBasic(
                    ChapterId: data.basic.chapterID,
                    Type: (QuestType) data.basic.questType,
                    Account: data.basic.account,
                    StandardLevel: data.basic.standardLevel,
                    Forfeitable: !data.basic.disableGiveup,
                    EventTag: data.basic.eventTag,
                    AutoStart: data.basic.autoStart,
                    Disabled: data.basic.locking,
                    UsePostbox: data.basic.usePostbox,
                    StartNpc: data.start?.npc ?? 0,
                    CompleteNpc: data.complete?.npc ?? 0,
                    CompleteMaps: data.complete?.map,
                    ProgressMaps: data.progressMap.progressMap
                ),
                Require: new QuestMetadataRequire(
                    Level: data.require.level,
                    MaxLevel: data.require.maxLevel,
                    Job: data.require.job.Select(job => (JobCode) job).ToArray(),
                    Quest: data.require.quest,
                    SelectableQuest: data.require.selectableQuest,
                    Achievement: data.require.achievement,
                    UnrequiredAchievement: unrequiredAchievement,
                    GearScore: data.require.gearScore
                ),
                AcceptReward: Convert(data.acceptReward),
                CompleteReward: Convert(data.completeReward),
                RemoteAccept: new QuestRemoteAccept(
                    Type: (QuestRemoteType) data.remoteAccept.useRemote,
                    MapId: data.remoteAccept.requireField
                ),
                RemoteComplete: new QuestRemoteComplete(
                    Type: (QuestRemoteType) data.remoteComplete.useRemote,
                    MapId: data.remoteComplete.requireField,
                    RequireDungeonClear: data.remoteComplete.requireDungeonClear > 0
                ),
                GoToNpc: new QuestMetadataGoToNpc(
                    Enabled: data.gotoNpc.enable,
                    MapId: data.gotoNpc.gotoField,
                    PortalId: data.gotoNpc.gotoPortal),
                GoToDungeon: new QuestMetadataGoToDungeon(
                    State: (QuestState) data.gotoDungeon.state,
                    MapId: data.gotoDungeon.gotoDungeon,
                    InstanceId: data.gotoDungeon.gotoInstanceID),
                Dispatch: data.dispatch == null ? null : new QuestDispatch(
                    Type: Enum.TryParse(data.dispatch.type, true, out QuestDispatchType dispatchType) ? dispatchType : QuestDispatchType.None,
                    MapId: data.dispatch.field,
                    PortalId: data.dispatch.portal,
                    Script: data.dispatch.script
                ),
                Mentoring: data.mentoringMission == null || string.IsNullOrEmpty(data.mentoringMission.mentoringIcon) ? null : new QuestMentoringMission(
                    OpeningDay: data.mentoringMission.openingDay,
                    Season: data.mentoringMission.mentoringSeason
                ),
                SummonPortal: data.summonPortal is { fieldID: 0, portalID: 0 } ? null : new QuestSummonPortal(
                    MapId: data.summonPortal.fieldID,
                    PortalId: data.summonPortal.portalID
                ),
                EventMissionType: Enum.TryParse(data.eventMission.@event, true, out QuestEventMissionType eventMissionType) ? eventMissionType : QuestEventMissionType.none,
                Conditions: data.condition.Select(condition => new ConditionMetadata(
                    Type: (ConditionType) condition.type,
                    Value: condition.value == 0 ? 1 : condition.value,
                    Codes: condition.code.ConvertCodes(),
                    Target: condition.target.ConvertCodes(),
                    PartyCount: condition.partyCount,
                    GuildPartyCount: condition.guildPartyCount
                )).ToArray()
            );
        }
    }

    private static QuestMetadataReward Convert(Reward reward) {
        List<Reward.Item> essentialItem = reward.essentialItem;
        List<Reward.Item> essentialJobItem = reward.essentialJobItem;
        if (FeatureLocaleFilter.FeatureEnabled("GlobalQuestRewardItem")) {
            essentialItem = reward.globalEssentialItem.Count > 0 ? reward.globalEssentialItem : essentialItem;
            essentialJobItem = reward.globalEssentialJobItem.Count > 0 ? reward.globalEssentialJobItem : essentialJobItem;
        }

        return new QuestMetadataReward(
            Meso: reward.money,
            Exp: reward.exp,
            RelativeExp: ToExpType(reward.relativeExp),
            GuildFund: reward.guildFund,
            GuildExp: reward.guildExp,
            GuildCoin: reward.guildCoin,
            Treva: reward.karma,
            Rue: reward.lu,
            MenteeCoin: reward.menteeCoin,
            MissionPoint: reward.missionPoint,
            EssentialItem: essentialItem.Select(item =>
                new QuestMetadataReward.Item(item.code, item.rank, item.count)).ToList(),
            EssentialJobItem: essentialJobItem.Select(item =>
                new QuestMetadataReward.Item(item.code, item.rank, item.count)).ToList()
        );
    }

    private static ExpType ToExpType(RelativeExp commonExpType) {
        if (Enum.TryParse(commonExpType.ToString(), out ExpType expType)) {
            return expType;
        }
        return ExpType.none;
    }

    /// <summary>
    /// QuestParser.Parse() builds this with Dictionary.Add and throws when the client ships
    /// both questdescription_final.xml and the per-category files it was compiled from,
    /// because they overlap. Merge them instead, letting the compiled file win.
    /// </summary>
    private Dictionary<int, string> ParseQuestNames() {
        var names = new Dictionary<int, string>();
        IEnumerable<PackFileEntry> files = xmlReader.Files
            .Where(entry => entry.Name.StartsWith($"string/{language}/questdescription", StringComparison.OrdinalIgnoreCase))
            // false sorts first, so the compiled file is read last and overwrites.
            .OrderBy(entry => entry.Name.Contains("_final", StringComparison.OrdinalIgnoreCase));

        foreach (PackFileEntry file in files) {
            using var reader = XmlReader.Create(new StringReader(Sanitizer.SanitizeQuestDescription(xmlReader.GetString(file))));
            if (descriptionSerializer.Deserialize(reader) is not QuestDescriptionRoot root) {
                continue;
            }

            foreach (QuestDescription description in root.quest) {
                names[description.questID] = description.name;
            }
        }

        return names;
    }

    private IEnumerable<(int Id, string Name, QuestData Data)> ParseQuests(IReadOnlyDictionary<int, string> questNames) {
        foreach (PackFileEntry file in xmlReader.Files.Where(entry => entry.Name.StartsWith("quest/"))) {
            using var reader = XmlReader.Create(new StringReader(Sanitizer.SanitizeQuest(xmlReader.GetString(file))));
            QuestData? data = (questSerializer.Deserialize(reader) as QuestDataRootRoot)?.environment?.quest;
            if (data is null) {
                continue;
            }

            int id = int.Parse(Path.GetFileNameWithoutExtension(file.Name));
            yield return (id, questNames.GetValueOrDefault(id, string.Empty), data);
        }
    }
}
