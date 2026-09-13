using Maple2.Model;
using Maple2.Model.Enum;
using Maple2.Model.Game;
using Maple2.Model.Metadata;
using Maple2.Server.Game.Model;
using Maple2.Server.Game.Session;

namespace Maple2.Server.Game.Util;

public static class NpcTalkUtil {
    public static ScriptState? GetInitialScriptType(GameSession session, ScriptStateType type, ScriptMetadata? metadata, FieldNpc npc) {
        int npcId = npc.Value.Id;
        switch (type) {
            case ScriptStateType.Script:
                if (metadata is null) {
                    return null;
                }
                // Check if player meets the requirements for the job script.
                ScriptState? jobScriptState = metadata.States.Values.FirstOrDefault(state => state.Type == ScriptStateType.Job);
                if (jobScriptState != null) {
                    if (!session.ServerTableMetadata.JobConditionTable.Entries.TryGetValue(metadata.Id, out JobConditionMetadata? jobCondition)) {
                        return jobScriptState;
                    }
                    if (MeetsJobCondition(session, jobCondition)) {
                        return jobScriptState;
                    }
                }

                List<ScriptState> scriptStates = [];
                session.ServerTableMetadata.ScriptConditionTable.Entries.TryGetValue(metadata.Id, out Dictionary<int, ScriptConditionMetadata>? scriptConditions);
                // Check if player meets the requirements for each pick script.
                foreach (ScriptState scriptState in metadata.States.Values.Where(state => state.Pick)) {
                    if (scriptConditions == null) {
                        scriptStates.Add(scriptState);
                        continue;
                    }

                    if (scriptState.JobCondition != null &&
                        scriptState.JobCondition != JobCode.None &&
                        scriptState.JobCondition != session.Player.Value.Character.Job.Code()) {
                        continue;
                    }

                    if (!scriptConditions.TryGetValue(scriptState.Id, out ScriptConditionMetadata? scriptCondition)) {
                        // Every owner facing maid branch is gated on maid_auth, so a branch
                        // with no condition at all is the one a visitor sees.
                        if (!IsMaidOwner(session, npc)) {
                            scriptStates.Add(scriptState);
                        }
                        continue;
                    }

                    if (scriptCondition.ConditionCheck(session, npc)) {
                        scriptStates.Add(scriptState);
                    }
                }

                return scriptStates.Count == 0 ? null : scriptStates[Random.Shared.Next(scriptStates.Count)];
            case ScriptStateType.Quest:
                SortedDictionary<int, QuestMetadata> quests = session.Quest.GetAvailableQuests(npcId);
                if (quests.Count == 0) {
                    return null;
                }

                if (!session.ScriptMetadata.TryGet(quests.Keys.Min(), out ScriptMetadata? questMetadata)) {
                    return null;
                }
                return GetQuestScriptState(session, questMetadata, npcId);
            case ScriptStateType.Select:
                if (metadata is null) {
                    return null;
                }
                List<ScriptState> selectScriptStates = [];
                session.ServerTableMetadata.ScriptConditionTable.Entries.TryGetValue(metadata.Id, out Dictionary<int, ScriptConditionMetadata>? selectScriptConditions);
                foreach (ScriptState scriptState in metadata.States.Values.Where(state => state.Type == ScriptStateType.Select)) {
                    if (selectScriptConditions == null) {
                        selectScriptStates.Add(scriptState);
                        continue;
                    }

                    if (!selectScriptConditions.TryGetValue(scriptState.Id, out ScriptConditionMetadata? scriptCondition)) {
                        if (!IsMaidOwner(session, npc)) {
                            selectScriptStates.Add(scriptState);
                        }
                        continue;
                    }

                    if (scriptCondition.ConditionCheck(session, npc)) {
                        selectScriptStates.Add(scriptState);
                    }
                }
                return selectScriptStates.Count == 0 ? null : selectScriptStates[Random.Shared.Next(selectScriptStates.Count)];
        }
        return null;
    }

    private static bool MeetsJobCondition(GameSession session, JobConditionMetadata jobCondition) {
        if (session.Field is null) return false;
        if (jobCondition.StartedQuestId > 0 &&
            (!session.Quest.TryGetQuest(jobCondition.StartedQuestId, out Quest? startedQuest) || startedQuest.State != QuestState.Started)) {
            return false;
        }

        if (jobCondition.CompletedQuestId > 0 &&
            (!session.Quest.TryGetQuest(jobCondition.CompletedQuestId, out Quest? completedQuest) || completedQuest.State != QuestState.Completed)) {
            return false;
        }

        if (jobCondition.JobCode != JobCode.None && session.Player.Value.Character.Job.Code() != jobCondition.JobCode) {
            return false;
        }

        // TODO: Maid checks

        if (jobCondition.BuffId > 0 && !session.Player.Buffs.HasBuff(jobCondition.BuffId)) {
            return false;
        }

        if (jobCondition.Mesos > 0 && session.Currency.Meso < jobCondition.Mesos) {
            return false;
        }

        if (jobCondition.Level > 0 && session.Player.Value.Character.Level < jobCondition.Level) {
            return false;
        }

        // TODO: Check if player is in home

        if (jobCondition.Guild && session.Player.Value.Character.GuildId == 0) {
            return false;
        }

        if (jobCondition.CompletedAchievement > 0 && !session.Achievement.HasAchievement(jobCondition.CompletedAchievement)) {
            return false;
        }

        // TODO: Check if it's the player's birthday

        if (jobCondition.MapId > 0 && session.Field?.MapId != jobCondition.MapId) {
            return false;
        }

        if (jobCondition.DeathPenalty && session.Config.DeathPenaltyEndTick < session.Field.FieldTick) {
            return false;
        }

        return true;
    }

    public static bool ConditionCheck(this ScriptConditionMetadata scriptCondition, GameSession session, FieldNpc? npc = null) {
        if (session.Field is null) return false;
        if (scriptCondition.JobCode.Count > 0 && !scriptCondition.JobCode.Contains(session.Player.Value.Character.Job.Code())) {
            return false;
        }

        foreach ((int questId, bool started) in scriptCondition.QuestStarted) {
            session.Quest.TryGetQuest(questId, out Quest? quest);
            if (started && quest is not { State: QuestState.Started }) {
                return false;
            }

            if (!started && quest is { State: QuestState.Started }) {
                return false;
            }
        }

        foreach ((int questId, bool completed) in scriptCondition.QuestCompleted) {
            session.Quest.TryGetQuest(questId, out Quest? quest);
            if (completed && quest is not { State: QuestState.Completed }) {
                return false;
            }

            if (!completed && quest is { State: QuestState.Completed }) {
                return false;
            }
        }

        foreach ((ItemComponent itemComponent, bool has) in scriptCondition.Items) {
            IEnumerable<Item> items = session.Item.Inventory.Find(itemComponent.ItemId, itemComponent.Rarity);
            int itemSum = items.Sum(item => item.Amount);
            if (has && itemSum < itemComponent.Amount) {
                return false;
            }

            if (!has && itemSum >= itemComponent.Amount) {
                return false;
            }
        }

        if (scriptCondition.Buff.Key > 0) {
            if (scriptCondition.Buff.Value && !session.Player.Buffs.HasBuff(scriptCondition.Buff.Key)) {
                return false;
            }
        }

        if (scriptCondition.Meso.Key > 0) {
            if (scriptCondition.Meso.Value && session.Currency.Meso < scriptCondition.Meso.Key) {
                return false;
            }

            if (!scriptCondition.Meso.Value && session.Currency.Meso >= scriptCondition.Meso.Key) {
                return false;
            }
        }

        if (scriptCondition.Level.Key > 0) {
            if (scriptCondition.Level.Value && session.Player.Value.Character.Level < scriptCondition.Level.Key) {
                return false;
            }

            if (!scriptCondition.Level.Value && session.Player.Value.Character.Level >= scriptCondition.Level.Key) {
                return false;
            }
        }

        if (scriptCondition.AchieveCompleted.Key > 0) {
            if (scriptCondition.AchieveCompleted.Value && !session.Achievement.HasAchievement(scriptCondition.AchieveCompleted.Key)) {
                return false;
            }

            if (!scriptCondition.AchieveCompleted.Value && session.Achievement.HasAchievement(scriptCondition.AchieveCompleted.Key)) {
                return false;
            }
        }

        if (scriptCondition.InGuild && session.Player.Value.Character.GuildId == 0) {
            return false;
        }

        if (scriptCondition.DeathPenalty && session.Config.DeathPenaltyEndTick < session.Field.FieldTick) {
            return false;
        }

        if (!MeetsMaidCondition(session, npc, scriptCondition.Maid)) {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Maid dialogue is split into branches by these conditions, so leaving them unchecked
    /// makes every branch match at once. Only the parts that can be derived from the maid
    /// data we decode are enforced; the rest are ignored rather than guessed.
    /// </summary>
    private static bool MeetsMaidCondition(GameSession session, FieldNpc? npc, ScriptConditionMetadata.MaidData condition) {
        bool checksMaid = condition.Authority
                          || condition.Expired.Key > 0
                          || condition.ReadyToPay.Key > 0
                          || condition.ClosenessRank > 0
                          || condition.ClosenessTime.Key > 0
                          || condition.MoodTime.Key > 0
                          || condition.DaysBeforeExpired.Key > 0;
        if (!checksMaid) {
            return true;
        }

        Maid? maid = npc?.Maid;
        if (maid is null) {
            // The script is asking about a maid but this npc is not one.
            return false;
        }

        if (condition.Authority && session.AccountId != maid.AccountId) {
            return false;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (condition.Expired.Key > 0 && now >= maid.ExpiryTime != condition.Expired.Value) {
            return false;
        }

        if (condition.DaysBeforeExpired.Key > 0) {
            long daysLeft = Math.Max(0, (maid.ExpiryTime - now) / (24 * 60 * 60));
            if (daysLeft <= condition.DaysBeforeExpired.Key != condition.DaysBeforeExpired.Value) {
                return false;
            }
        }

        if (condition.ReadyToPay.Key > 0) {
            // The contract can be renewed over its last MaidReadyToPay days, as the housing
            // tool tells the player. Paying pushes the expiry out, which closes the window
            // again on its own.
            long window = session.ServerTableMetadata.ConstantsTable.MaidReadyToPay * 24L * 60 * 60;
            if (now >= maid.ExpiryTime - window != condition.ReadyToPay.Value) {
                return false;
            }
        }

        // ClosenessRank, ClosenessTime and MoodTime need maid fields that are still
        // undecoded, so they stay unchecked until those are identified.
        return true;
    }

    private static bool IsMaidOwner(GameSession session, FieldNpc? npc) {
        return npc?.Maid is not null && session.AccountId == npc.Maid.AccountId;
    }

    public static ScriptState? GetQuestScriptState(GameSession session, ScriptMetadata? scriptMetadata, int npcId) {
        if (scriptMetadata is null) {
            return null;
        }
        session.Quest.TryGetQuest(scriptMetadata.Id, out Quest? quest);
        QuestState questState = quest?.State ?? QuestState.None;

        int stateId = questState switch {
            QuestState.None => GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 100, 200),
            QuestState.Started => GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 200, 300),
            _ => 0,
        };

        if (quest != null && session.Quest.CanStart(quest.Metadata)) {
            stateId = GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 200, 300);
        }

        if (quest != null && quest.Metadata.Basic.CompleteNpc == npcId) {
            stateId = GetFirstStateScript(scriptMetadata.States.Keys.ToArray(), 300, 400);
        }

        return scriptMetadata.States.TryGetValue(stateId, out ScriptState? scriptState) ? scriptState : null;
    }

    public static int GetFirstStateScript(IEnumerable<int> questStates, int lowerBound, int upperBound) {
        IEnumerable<int> statesInRange = questStates.Where(id => id >= lowerBound && id <= upperBound);
        return statesInRange.Min();
    }
}
