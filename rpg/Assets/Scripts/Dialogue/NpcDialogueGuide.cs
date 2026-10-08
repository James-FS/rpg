using System;
using UnityEngine;
using FogHarbor.Quest;

namespace FogHarbor.Dialogue
{
    public enum DialogueTopicAction { Speak, AcceptQuest, CompleteQuest }
    [Serializable]
    public class DialogueTopic
    {
        public string label;
        [TextArea] public string prompt;
        public DialogueTopicAction action;
    }
    [CreateAssetMenu(menuName = "FogHarbor/NPC Dialogue Guide")]
    public class NpcDialogueGuide : ScriptableObject
    {
        public string npcId;
        public string role;
        public QuestData quest;
        [TextArea] public string availableGreeting, acceptedGreeting, completedGreeting, rewardedGreeting;
        [TextArea] public string acceptedResponse, completedResponse;
        public DialogueTopic[] availableTopics, acceptedTopics, completedTopics, rewardedTopics;
        public string Greeting(QuestState state)
        {
            switch (state) {
                case QuestState.Accepted: return acceptedGreeting;
                case QuestState.Completed: return completedGreeting;
                case QuestState.Rewarded: return rewardedGreeting;
                default: return availableGreeting;
            }
        }
        public DialogueTopic[] Topics(QuestState state)
        {
            switch (state) {
                case QuestState.Accepted: return acceptedTopics;
                case QuestState.Completed: return completedTopics;
                case QuestState.Rewarded: return rewardedTopics;
                default: return availableTopics;
            }
        }
    }
}