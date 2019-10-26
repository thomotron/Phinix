// Generated by the protocol buffer compiler.  DO NOT EDIT!
// source: Stores/ChatHistoryStore.proto
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Chat {

  /// <summary>Holder for reflection information generated from Stores/ChatHistoryStore.proto</summary>
  public static partial class ChatHistoryStoreReflection {

    #region Descriptor
    /// <summary>File descriptor for Stores/ChatHistoryStore.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static ChatHistoryStoreReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Ch1TdG9yZXMvQ2hhdEhpc3RvcnlTdG9yZS5wcm90bxIEQ2hhdBodU3RvcmVz",
            "L0NoYXRNZXNzYWdlU3RvcmUucHJvdG8iQAoQQ2hhdEhpc3RvcnlTdG9yZRIs",
            "CgxDaGF0TWVzc2FnZXMYASADKAsyFi5DaGF0LkNoYXRNZXNzYWdlU3RvcmVi",
            "BnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Chat.ChatMessageStoreReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Chat.ChatHistoryStore), global::Chat.ChatHistoryStore.Parser, new[]{ "ChatMessages" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class ChatHistoryStore : pb::IMessage<ChatHistoryStore> {
    private static readonly pb::MessageParser<ChatHistoryStore> _parser = new pb::MessageParser<ChatHistoryStore>(() => new ChatHistoryStore());
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<ChatHistoryStore> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Chat.ChatHistoryStoreReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ChatHistoryStore() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ChatHistoryStore(ChatHistoryStore other) : this() {
      chatMessages_ = other.chatMessages_.Clone();
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ChatHistoryStore Clone() {
      return new ChatHistoryStore(this);
    }

    /// <summary>Field number for the "ChatMessages" field.</summary>
    public const int ChatMessagesFieldNumber = 1;
    private static readonly pb::FieldCodec<global::Chat.ChatMessageStore> _repeated_chatMessages_codec
        = pb::FieldCodec.ForMessage(10, global::Chat.ChatMessageStore.Parser);
    private readonly pbc::RepeatedField<global::Chat.ChatMessageStore> chatMessages_ = new pbc::RepeatedField<global::Chat.ChatMessageStore>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::Chat.ChatMessageStore> ChatMessages {
      get { return chatMessages_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as ChatHistoryStore);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(ChatHistoryStore other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if(!chatMessages_.Equals(other.chatMessages_)) return false;
      return true;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      hash ^= chatMessages_.GetHashCode();
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      chatMessages_.WriteTo(output, _repeated_chatMessages_codec);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      size += chatMessages_.CalculateSize(_repeated_chatMessages_codec);
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(ChatHistoryStore other) {
      if (other == null) {
        return;
      }
      chatMessages_.Add(other.chatMessages_);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            input.SkipLastField();
            break;
          case 10: {
            chatMessages_.AddEntriesFrom(input, _repeated_chatMessages_codec);
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code