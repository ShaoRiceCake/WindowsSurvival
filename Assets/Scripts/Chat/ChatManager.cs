using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;

public class ChatManager : MonoBehaviour
{
    #region 单例

    private static ChatManager instance;

    public static ChatManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ChatManager>();
                if (instance == null)
                {
                    GameObject managerObj = new GameObject("ChatManager");
                    instance = managerObj.AddComponent<ChatManager>();
                }
            }

            return instance;
        }
    }

    #endregion

    public ChatWindow chatWindow;
    public HoverableButton chatSpeedButton;
    private int curSpeed = 1;
    private int appliedDefaultSpeed;
    public bool AwaitingDelivery { get; private set; }

    #region 数据

    //已生成的对话列表
    public List<ChatData> GeneratedChatDataList = new List<ChatData>();

    //需要触发的段落列表(存储段落名)
    public List<string> ParagraphToTriggeer = new List<string>();

    //当前段落数据
    public ParagraphData CurrentParagraphData=>ReadChatParagraph.Instance.CurGraphData.paragraphData;

    //当前选项数据
    public string ChoosedChatData;

    //是否在段落中
    public bool inParagraph = false;

    //打断的段落数据
    public ParagraphData InterruptParagraphData = null;

    //当前是否在选择中
    public bool Choosing = false;

    #endregion

    private void Awake()
    {
        // 确保只有一个实例
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        //添加对话段落触发监听
        EventManager.Instance.AddListener<ParagraphData>(EventType.TriggerParagraph, TriggerParagraph);
        if (!GameDataManager.Instance.GeneratedChatData.init)
        {
            if (!GameDataManager.Instance.CurLoad.skipGuide)
            {
                ParagraphToTriggeer.Add("一切的开始");
            }
        }
        else
        {
            GeneratedChatDataList = GameDataManager.Instance.GeneratedChatData.GeneratedChatDataList;
            ParagraphToTriggeer = GameDataManager.Instance.GeneratedChatData.ParagraphToTriggeer;
            inParagraph = GameDataManager.Instance.GeneratedChatData.inParagraph;
            InterruptParagraphData = GameDataManager.Instance.GeneratedChatData.InterruptParagraphData;
            Choosing = GameDataManager.Instance.GeneratedChatData.Choosing;
        }
    }

    private void Start()
    {
        ApplyDefaultSpeed();
        GameSettings.Changed += ApplyDefaultSpeed;
        chatSpeedButton.onClick.AddListener(() =>
        {
            if (curSpeed == 1)
            {
                ChangeChatSpeed(3);
            }
            else if (curSpeed == 3)
            {
                ChangeChatSpeed(10);
            }
            else
            {
                ChangeChatSpeed(1);
            }
        });
    }

    private void ApplyDefaultSpeed()
    {
        if (appliedDefaultSpeed == GameSettings.Current.dialogueSpeed) return;
        appliedDefaultSpeed = GameSettings.Current.dialogueSpeed;
        ChangeChatSpeed(appliedDefaultSpeed);
    }

    public void OnDestroy()
    {
        GameSettings.Changed -= ApplyDefaultSpeed;
        //移除对话段落监听
        EventManager.Instance.RemoveListener<ParagraphData>(EventType.TriggerParagraph, TriggerParagraph);
    }

    public void InitChat()
    {
        if (GeneratedChatDataList.Count > 0)
        {
            LoadGeneratedChatData();
        }
        else if (ParagraphToTriggeer.Count > 0)
        {
            NextParagraph();
        }
    }

    //触发段落时判断是否需要打断，不打断则放弃该段对话
    public void AddTriggerParagraph(ParagraphData paragraphData)
    {
        //当前在段落内
        if (inParagraph)
        {
            //判断是否可以打断，无法打断则加入待触发列表，在本段对话结束后触发
            if (paragraphData.ParagraphPriority > CurrentParagraphData.ParagraphPriority)
            {
                InterruptParagraphData = paragraphData;
                //如果当前在等待选择则删除选项，直接进入对话
                if (Choosing)
                {
                    ChoosedChatData = null;
                    chatWindow.InterruptChoose();
                    Choosing = false;
                    TriggerMessage(null);
                }
            }
            else
            {
                ParagraphToTriggeer.Add(paragraphData.ParagraphName);
            }
        }
        else
        {
            TriggerParagraph(paragraphData);
        }
    }

    public void TriggerParagraph(ParagraphData paragraphData)
    {
        inParagraph = true;
        ReadChatParagraph.Instance.FindStartNodeOfParagraph(paragraphData.ParagraphName);
        TriggerMessage(ReadChatParagraph.Instance.CurNode);
    }

    //生成所有被记录的数据
    public void LoadGeneratedChatData()
    {
        //进入对话
        inParagraph = true;
        //从GeneratedChatDataList中加载所有已触发的对话数据
        for (int i = 0; i < GeneratedChatDataList.Count; i++)
        {
            chatWindow.CreateMessage(GeneratedChatDataList[i].MessageSender, GeneratedChatDataList[i].Message);
        }
        //触发下一个对话（找到当前节点的下一句，如果最后一句是选项或分支需要重新触发最后一句的效果）
        if (ReadChatParagraph.Instance.CurNode.typeName == "End")
        {
            NextParagraph();
        }
        else if(ReadChatParagraph.Instance.CurNode.typeName=="Choose"||ReadChatParagraph.Instance.CurNode.typeName=="BranchCondition")
        {
            TriggerMessage(ReadChatParagraph.Instance.CurNode);
        }
        else
        {
            TriggerMessage(ReadChatParagraph.Instance.FindNextNode());
        }

    }

    //根据下一条消息的类型决定触发消息类型为选项还是消息
    public void TriggerMessage(GraphData.SerializedNode nodeData)
    {
        //如果打断对话非空时触发打断对话
        if (InterruptParagraphData != null)
        {
            ParagraphData tmpParagraph = InterruptParagraphData;
            InterruptParagraphData = null;
            TriggerParagraph(tmpParagraph);
            return;
        }

        //如果需要判断通过条件
        if (nodeData.chatData.MessageCondition != "")
        {
            inParagraph = false;
            ChatConditionManager.Instance.StartChatConditionDetection(nodeData);
            return;
        }

        //根据类型生成消息
        switch (nodeData.typeName)
        {
            case "Dialogue":
                if (nodeData.chatData.MessageCondition != "")
                {
                    inParagraph = false;
                    ChatConditionManager.Instance.StartChatConditionDetection(nodeData);
                    return;
                }
                CreateMessage(nodeData.chatData);
                break;
            case "Choose":
                Choosing = true;
                chatWindow.SetDialogueOptions(nodeData);
                break;
            case "BranchCondition":
                // 先收集所有选项消息
                foreach (var portData in nodeData.outputports)
                {
                    if (portData.name != "" && ChatConditionManager.Instance.CanTriggerBranchCondition(portData.name))
                    {
                        TriggerMessage(ReadChatParagraph.Instance.FindNextNode(portData.name));
                        break;
                    }
                }
                break;
            case "End":
                NextParagraph();
                break;
            case "Start":
                TriggerMessage(ReadChatParagraph.Instance.FindNextNode());
                break;
        }
    }

    public void AddToGenerated(ChatData chatData)
    {
        GeneratedChatDataList.Add(chatData);
        if (GeneratedChatDataList.Count > 20)
        {
            GeneratedChatDataList.RemoveAt(0);
            chatWindow.RemoveFirstMessage();
        }
    }

    public void CreateMessage(ChatData chatData, float waitTime = -1f)
    {
        StartCoroutine(CreateMessageCoroutine(chatData, waitTime));
    }

    //创建消息（不包括选项）
    private IEnumerator CreateMessageCoroutine(ChatData chatData, float waitTime)
    {

        float finalWaitTime;

        if (waitTime > 0) finalWaitTime = waitTime;
        else finalWaitTime = chatData.preWaitTime == 0 ? 0.2f : chatData.preWaitTime;

        finalWaitTime /= curSpeed;

        AwaitingDelivery = true;
        yield return new WaitForSeconds(finalWaitTime);
        AwaitingDelivery = false;

        //将该对话加入已生成列表
        AddToGenerated(chatData);
        chatWindow.CreateMessage(chatData.MessageSender, chatData.Message);

        SoundManager.Instance.PlaySound("消息提示音_02", true);
        //触发对话效果
        AfterChatFactory.TriggerEffect(chatData.TriggerMessageEffect);

        //消息前置等待时间
        if (waitTime > 0) finalWaitTime = waitTime;
        else finalWaitTime = chatData.lateWaitTime == 0 ? 2.1f : chatData.lateWaitTime;
        finalWaitTime /= curSpeed;
        yield return new WaitForSeconds(finalWaitTime);

        TriggerMessage(ReadChatParagraph.Instance.FindNextNode());

    }

    public void NextParagraph()
    {
        if (ParagraphToTriggeer.Count > 0)
        {
            ParagraphData tmpParagraphData=ReadChatParagraph.Instance.FindParagraphDataByName(ParagraphToTriggeer[0]);
            TriggerParagraph(tmpParagraphData);
            ParagraphToTriggeer.RemoveAt(0);
        }
        else
        {
            inParagraph = false;
        }
    }

    public void Submit()
    {
        if (ChoosedChatData == null) return;
        Choosing = false;
        TriggerMessage(ReadChatParagraph.Instance.FindNextNode(ChoosedChatData));
        ChoosedChatData = null;
    }

    private void ChangeChatSpeed(int speed)
    {
        curSpeed = speed;
        chatSpeedButton.GetComponentInChildren<Text>().text = $"x{speed}";
    }

    public void ReturnToMainMenuAndDeleteSave()
    {
        SaveSystem.Die();
    }
}
