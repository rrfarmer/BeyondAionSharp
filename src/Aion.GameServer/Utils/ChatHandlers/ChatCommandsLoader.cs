using System;
using Aion.Commons.Scripting.ClassListener;

namespace Aion.GameServer.Utils.ChatHandlers;

/// <summary>Java parity: utils/chathandlers/ChatCommandsLoader (Aquanox). Script class-listener that instantiates and registers ChatCommand subclasses on load. ClassListener (scripting pillar) red-tolerated.</summary>
public class ChatCommandsLoader : ClassListener
{
    private ChatProcessor processor;

    public ChatCommandsLoader(ChatProcessor processor)
    {
        this.processor = processor;
    }

    public void PostLoad(Type[] classes)
    {
        foreach (Type c in classes)
        {
            if (!IsValidClass(c))
                continue;
            try
            {
                processor.RegisterCommand((ChatCommand)Activator.CreateInstance(c));
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e);
            }
        }
    }

    public void PreUnload(Type[] classes)
    {
    }

    public bool IsValidClass(Type clazz)
    {
        if (clazz.IsAbstract || clazz.IsInterface)
            return false;

        if (!clazz.IsPublic)
            return false;

        return typeof(ChatCommand).IsAssignableFrom(clazz);
    }
}
