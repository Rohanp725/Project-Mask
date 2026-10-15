using System;
using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Core
{
    /// <summary>
    /// Stack of open modal screens (dialogue, notebook, archive, menus). While anything is open the player
    /// can't move or look and the cursor is free. Esc closes the topmost screen.
    /// </summary>
    public static class InputLock
    {
        private sealed class Entry
        {
            public object Owner;
            public Action OnEscape;
        }

        private static readonly List<Entry> Stack = new List<Entry>();

        public static bool IsLocked => Stack.Count > 0;
        public static object Top => Stack.Count > 0 ? Stack[Stack.Count - 1].Owner : null;
        public static event Action Changed;

        public static void Push(object owner, Action onEscape)
        {
            if (Stack.Exists(e => e.Owner == owner))
                return;
            Stack.Add(new Entry { Owner = owner, OnEscape = onEscape });
            Apply();
        }

        public static void Pop(object owner)
        {
            int index = Stack.FindIndex(e => e.Owner == owner);
            if (index < 0)
                return;
            Stack.RemoveAt(index);
            Apply();
        }

        public static bool IsOpen(object owner) => Stack.Exists(e => e.Owner == owner);

        /// <summary>Closes the topmost screen. Returns false if nothing was open.</summary>
        public static bool EscapeTop()
        {
            if (Stack.Count == 0)
                return false;
            var top = Stack[Stack.Count - 1];
            // No handler means the screen can't be escaped (a fade, a death, an ending): swallow the key.
            top.OnEscape?.Invoke();
            return true;
        }

        private static void Apply()
        {
            bool locked = IsLocked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            Stack.Clear();
            Changed = null;
        }
    }
}
