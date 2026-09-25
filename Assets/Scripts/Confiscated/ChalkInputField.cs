using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Confiscated
{
    /// <summary>UGUI caret/selection with the project's new Input System, without duplicate legacy key events.</summary>
    public class ChalkInputField : InputField
    {
        Keyboard keyboard;
        Key repeating;
        float repeatAt;
        protected override void OnEnable(){base.OnEnable();Bind();}
        protected override void OnDisable(){if(keyboard!=null)keyboard.onTextInput-=Type;keyboard=null;base.OnDisable();}
        void Bind(){if(keyboard==Keyboard.current)return;if(keyboard!=null)keyboard.onTextInput-=Type;keyboard=Keyboard.current;if(keyboard!=null)keyboard.onTextInput+=Type;}
        EventModifiers Modifiers()
        {
            EventModifiers m=0;if(keyboard.shiftKey.isPressed)m|=EventModifiers.Shift;
            if(keyboard.ctrlKey.isPressed)m|=EventModifiers.Control;if(keyboard.altKey.isPressed)m|=EventModifiers.Alt;
            if(keyboard.leftMetaKey.isPressed||keyboard.rightMetaKey.isPressed)m|=EventModifiers.Command;return m;
        }
        void Type(char c)
        {
            if(!isFocused||!interactable||char.IsControl(c))return;
            if(keyboard.ctrlKey.isPressed||keyboard.leftMetaKey.isPressed||keyboard.rightMetaKey.isPressed)return;
            ProcessEvent(new Event{type=EventType.KeyDown,character=c,modifiers=Modifiers()});ForceLabelUpdate();
        }
        public override void OnUpdateSelected(BaseEventData eventData)
        {
            Bind();if(!isFocused||keyboard==null)return;
            Handle(Key.Backspace,KeyCode.Backspace);Handle(Key.Delete,KeyCode.Delete);
            Handle(Key.LeftArrow,KeyCode.LeftArrow);Handle(Key.RightArrow,KeyCode.RightArrow);Handle(Key.UpArrow,KeyCode.UpArrow);Handle(Key.DownArrow,KeyCode.DownArrow);
            Handle(Key.Home,KeyCode.Home);Handle(Key.End,KeyCode.End);Handle(Key.Enter,KeyCode.Return);Handle(Key.NumpadEnter,KeyCode.KeypadEnter);
            if(keyboard.ctrlKey.isPressed||keyboard.leftMetaKey.isPressed||keyboard.rightMetaKey.isPressed)
            {Handle(Key.A,KeyCode.A);Handle(Key.C,KeyCode.C);Handle(Key.V,KeyCode.V);Handle(Key.X,KeyCode.X);}
            eventData.Use();
        }
        void Handle(Key key,KeyCode code)
        {
            var control=keyboard[key];bool fire=control.wasPressedThisFrame;
            if(fire){repeating=key;repeatAt=Time.unscaledTime+.45f;}
            else if(control.isPressed&&repeating==key&&Time.unscaledTime>=repeatAt){fire=true;repeatAt=Time.unscaledTime+.045f;}
            if(!fire)return;ProcessEvent(new Event{type=EventType.KeyDown,keyCode=code,character=key==Key.Enter||key==Key.NumpadEnter?'\n':'\0',modifiers=Modifiers()});ForceLabelUpdate();
        }
    }
}
