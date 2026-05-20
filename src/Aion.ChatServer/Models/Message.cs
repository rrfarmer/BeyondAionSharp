using System.Text;
using Aion.ChatServer.Models.Channels;

namespace Aion.ChatServer.Models;

public sealed class Message
{
	public Message(Channel channel, byte[] text, ChatClient sender)
	{
		Channel = channel;
		Text = text;
		Sender = sender;
	}

	public Channel Channel { get; }

	public byte[] Text { get; private set; }

	public ChatClient Sender { get; }

	public int Size => Text.Length;

	public string TextString => Encoding.Unicode.GetString(Text);

	public void SetText(string text)
	{
		Text = Encoding.Unicode.GetBytes(text);
	}
}
