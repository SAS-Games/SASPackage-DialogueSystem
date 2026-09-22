using System.Collections;
using UnityEngine;

namespace SAS.DialogueSystem.Samples
{
    public sealed class DialogueSampleBootstrap : MonoBehaviour
    {
        [SerializeField] private DialogueHandler m_Handler;
        [SerializeField] private TextAsset m_InkJson;
        [SerializeField] private DialogueMetadataProfile m_MetadataProfile;

        private IEnumerator Start()
        {
            yield return null;

            if (m_Handler == null)
                m_Handler = FindFirstObjectByType<DialogueHandler>();

            if (m_Handler == null || m_InkJson == null)
            {
                Debug.LogError("The dialogue sample requires a DialogueHandler and compiled Ink JSON.", this);
                yield break;
            }

            m_Handler.EnterDialogueMode(m_InkJson, gameObject, m_MetadataProfile);
        }
    }
}