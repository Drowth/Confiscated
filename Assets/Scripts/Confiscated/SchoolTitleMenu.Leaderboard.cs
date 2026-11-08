using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Confiscated
{
    /// <summary>
    /// Title screen Steam leaderboards: one card, a tab per ranked start, top ten in two columns. It only reads boards
    /// (SteamLeaderboard.Browse); times are uploaded by GameManager on a valid escape. Hidden in the itch build.
    /// </summary>
    public sealed partial class SchoolTitleMenu
    {
        GameObject boardPanel;Button boardBack;Text boardStatus,boardLeft,boardRight;
        readonly Image[] boardTabs=new Image[3];
        int boardTab;
        static readonly string[] BoardTabLabels={"PHONE START","LESSON START","RETRY START"};

        void BuildLeaderboard(Transform parent)
        {
            var dim=ImageRect("Leaderboard",parent,Vector2.zero,Vector2.one,new Color(0,0,0,.72f));boardPanel=dim.gameObject;
            var bg=ImageRect("Leaderboard card",dim.transform,new Vector2(.2f,.13f),new Vector2(.8f,.87f),Cream);
            var border=Rect("Pencil edge",bg.transform,Vector2.zero,Vector2.one).gameObject.AddComponent<SketchBorder>();border.color=Ink;border.raycastTarget=false;
            var p=bg.transform;
            Label("STEAM LEADERBOARDS",p,new Vector2(.05f,.88f),new Vector2(.95f,.96f),40,Ink);
            for(int i=0;i<3;i++)
            {
                int tab=i;float x=.05f+i*.305f;
                var b=MakeButton(BoardTabLabels[i],p,new Vector2(x,.76f),new Vector2(x+.29f,.85f),()=>ShowBoard(tab));
                b.GetComponentInChildren<Text>().fontSize=22;boardTabs[i]=(Image)b.targetGraphic;
            }
            boardStatus=Label("",p,new Vector2(.05f,.67f),new Vector2(.95f,.74f),22,Ink);boardStatus.alignment=TextAnchor.MiddleCenter;boardStatus.supportRichText=false;
            // Steam names are other people's text: no rich text.
            boardLeft=Label("",p,new Vector2(.05f,.2f),new Vector2(.5f,.66f),24,Ink);boardLeft.alignment=TextAnchor.UpperLeft;boardLeft.supportRichText=false;
            boardRight=Label("",p,new Vector2(.52f,.2f),new Vector2(.97f,.66f),24,Ink);boardRight.alignment=TextAnchor.UpperLeft;boardRight.supportRichText=false;
            boardBack=MakeButton("Back",p,new Vector2(.05f,.05f),new Vector2(.3f,.16f),()=>ShowLeaderboard(false));
            Label("Best escape times. Steam keeps your fastest.",p,new Vector2(.33f,.05f),new Vector2(.95f,.16f),18,Ink);
            boardPanel.SetActive(false);
        }
        public bool LeaderboardOpen=>boardPanel!=null&&boardPanel.activeSelf;
        public void ShowLeaderboard(bool show)
        {
            if(boardPanel==null)return;boardPanel.SetActive(show);
            if(show){SteamLeaderboard.Changed-=RedrawBoard;SteamLeaderboard.Changed+=RedrawBoard;ShowBoard(boardTab);EventSystem.current?.SetSelectedGameObject(boardBack.gameObject);}
            else{SteamLeaderboard.Changed-=RedrawBoard;EventSystem.current?.SetSelectedGameObject(startButton.gameObject);}
        }
        void ShowBoard(int tab)
        {
            boardTab=tab;
            for(int i=0;i<3;i++)boardTabs[i].color=i==tab?new Color(1,.87f,.51f):Cream;
            SteamLeaderboard.Browse(SteamLeaderboard.Categories[tab]);RedrawBoard();
        }
        void RedrawBoard()
        {
            if(boardStatus==null)return;
            boardStatus.text=SteamLeaderboard.Status;
            var top=SteamLeaderboard.Top;
            var a=new System.Text.StringBuilder();var b=new System.Text.StringBuilder();
            for(int i=0;i<top.Count&&i<10;i++)
                (i<5?a:b).Append(top[i].you?"> ":"   ").Append(top[i].rank).Append(".  ").Append(top[i].name).Append("   ").Append(RunTiming.Format(top[i].ms)).Append('\n');
            boardLeft.text=a.ToString();boardRight.text=b.ToString();
        }
    }
}
