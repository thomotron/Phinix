// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: Packets/CreateTradePacket.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Trading {

  /// <summary>Holder for reflection information generated from Packets/CreateTradePacket.proto</summary>
  public static partial class CreateTradePacketReflection {

    #region Descriptor
    /// <summary>File descriptor for Packets/CreateTradePacket.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static CreateTradePacketReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "Ch9QYWNrZXRzL0NyZWF0ZVRyYWRlUGFja2V0LnByb3RvEgdUcmFkaW5nIkwK",
            "EUNyZWF0ZVRyYWRlUGFja2V0EhEKCVNlc3Npb25JZBgBIAEoCRIMCgRVdWlk",
            "GAIgASgJEhYKDk90aGVyUGFydHlVdWlkGAMgASgJYgZwcm90bzM="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Trading.CreateTradePacket), global::Trading.CreateTradePacket.Parser, new[]{ "SessionId", "Uuid", "OtherPartyUuid" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class CreateTradePacket : pb::IMessage<CreateTradePacket> {
    private static readonly pb::MessageParser<CreateTradePacket> _parser = new pb::MessageParser<CreateTradePacket>(() => new CreateTradePacket());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<CreateTradePacket> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Trading.CreateTradePacketReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CreateTradePacket() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CreateTradePacket(CreateTradePacket other) : this() {
      sessionId_ = other.sessionId_;
      uuid_ = other.uuid_;
      otherPartyUuid_ = other.otherPartyUuid_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CreateTradePacket Clone() {
      return new CreateTradePacket(this);
    }

    /// <summary>Field number for the "SessionId" field.</summary>
    public const int SessionIdFieldNumber = 1;
    private string sessionId_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string SessionId {
      get { return sessionId_; }
      set {
        sessionId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "Uuid" field.</summary>
    public const int UuidFieldNumber = 2;
    private string uuid_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string Uuid {
      get { return uuid_; }
      set {
        uuid_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "OtherPartyUuid" field.</summary>
    public const int OtherPartyUuidFieldNumber = 3;
    private string otherPartyUuid_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string OtherPartyUuid {
      get { return otherPartyUuid_; }
      set {
        otherPartyUuid_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as CreateTradePacket);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(CreateTradePacket other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (SessionId != other.SessionId) return false;
      if (Uuid != other.Uuid) return false;
      if (OtherPartyUuid != other.OtherPartyUuid) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (SessionId.Length != 0) hash ^= SessionId.GetHashCode();
      if (Uuid.Length != 0) hash ^= Uuid.GetHashCode();
      if (OtherPartyUuid.Length != 0) hash ^= OtherPartyUuid.GetHashCode();
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      if (SessionId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(SessionId);
      }
      if (Uuid.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(Uuid);
      }
      if (OtherPartyUuid.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(OtherPartyUuid);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (SessionId.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(SessionId);
      }
      if (Uuid.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(Uuid);
      }
      if (OtherPartyUuid.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(OtherPartyUuid);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(CreateTradePacket other) {
      if (other == null) {
        return;
      }
      if (other.SessionId.Length != 0) {
        SessionId = other.SessionId;
      }
      if (other.Uuid.Length != 0) {
        Uuid = other.Uuid;
      }
      if (other.OtherPartyUuid.Length != 0) {
        OtherPartyUuid = other.OtherPartyUuid;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            SessionId = input.ReadString();
            break;
          }
          case 18: {
            Uuid = input.ReadString();
            break;
          }
          case 26: {
            OtherPartyUuid = input.ReadString();
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
