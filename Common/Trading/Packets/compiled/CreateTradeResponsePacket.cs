// Generated by the protocol buffer compiler.  DO NOT EDIT!
// source: Packets/CreateTradeResponsePacket.proto
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Trading {

  /// <summary>Holder for reflection information generated from Packets/CreateTradeResponsePacket.proto</summary>
  public static partial class CreateTradeResponsePacketReflection {

    #region Descriptor
    /// <summary>File descriptor for Packets/CreateTradeResponsePacket.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static CreateTradeResponsePacketReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CidQYWNrZXRzL0NyZWF0ZVRyYWRlUmVzcG9uc2VQYWNrZXQucHJvdG8SB1Ry",
            "YWRpbmcaIFBhY2tldHMvVHJhZGVGYWlsdXJlUmVhc29uLnByb3RvIqEBChlD",
            "cmVhdGVUcmFkZVJlc3BvbnNlUGFja2V0Eg8KB1N1Y2Nlc3MYASABKAgSDwoH",
            "VHJhZGVJZBgCIAEoCRIWCg5PdGhlclBhcnR5VXVpZBgDIAEoCRIyCg1GYWls",
            "dXJlUmVhc29uGAQgASgOMhsuVHJhZGluZy5UcmFkZUZhaWx1cmVSZWFzb24S",
            "FgoORmFpbHVyZU1lc3NhZ2UYBSABKAliBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Trading.TradeFailureReasonReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Trading.CreateTradeResponsePacket), global::Trading.CreateTradeResponsePacket.Parser, new[]{ "Success", "TradeId", "OtherPartyUuid", "FailureReason", "FailureMessage" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class CreateTradeResponsePacket : pb::IMessage<CreateTradeResponsePacket> {
    private static readonly pb::MessageParser<CreateTradeResponsePacket> _parser = new pb::MessageParser<CreateTradeResponsePacket>(() => new CreateTradeResponsePacket());
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<CreateTradeResponsePacket> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Trading.CreateTradeResponsePacketReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CreateTradeResponsePacket() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CreateTradeResponsePacket(CreateTradeResponsePacket other) : this() {
      success_ = other.success_;
      tradeId_ = other.tradeId_;
      otherPartyUuid_ = other.otherPartyUuid_;
      failureReason_ = other.failureReason_;
      failureMessage_ = other.failureMessage_;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CreateTradeResponsePacket Clone() {
      return new CreateTradeResponsePacket(this);
    }

    /// <summary>Field number for the "Success" field.</summary>
    public const int SuccessFieldNumber = 1;
    private bool success_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Success {
      get { return success_; }
      set {
        success_ = value;
      }
    }

    /// <summary>Field number for the "TradeId" field.</summary>
    public const int TradeIdFieldNumber = 2;
    private string tradeId_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string TradeId {
      get { return tradeId_; }
      set {
        tradeId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
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

    /// <summary>Field number for the "FailureReason" field.</summary>
    public const int FailureReasonFieldNumber = 4;
    private global::Trading.TradeFailureReason failureReason_ = 0;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public global::Trading.TradeFailureReason FailureReason {
      get { return failureReason_; }
      set {
        failureReason_ = value;
      }
    }

    /// <summary>Field number for the "FailureMessage" field.</summary>
    public const int FailureMessageFieldNumber = 5;
    private string failureMessage_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string FailureMessage {
      get { return failureMessage_; }
      set {
        failureMessage_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as CreateTradeResponsePacket);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(CreateTradeResponsePacket other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Success != other.Success) return false;
      if (TradeId != other.TradeId) return false;
      if (OtherPartyUuid != other.OtherPartyUuid) return false;
      if (FailureReason != other.FailureReason) return false;
      if (FailureMessage != other.FailureMessage) return false;
      return true;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (Success != false) hash ^= Success.GetHashCode();
      if (TradeId.Length != 0) hash ^= TradeId.GetHashCode();
      if (OtherPartyUuid.Length != 0) hash ^= OtherPartyUuid.GetHashCode();
      if (FailureReason != 0) hash ^= FailureReason.GetHashCode();
      if (FailureMessage.Length != 0) hash ^= FailureMessage.GetHashCode();
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      if (Success != false) {
        output.WriteRawTag(8);
        output.WriteBool(Success);
      }
      if (TradeId.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(TradeId);
      }
      if (OtherPartyUuid.Length != 0) {
        output.WriteRawTag(26);
        output.WriteString(OtherPartyUuid);
      }
      if (FailureReason != 0) {
        output.WriteRawTag(32);
        output.WriteEnum((int) FailureReason);
      }
      if (FailureMessage.Length != 0) {
        output.WriteRawTag(42);
        output.WriteString(FailureMessage);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (Success != false) {
        size += 1 + 1;
      }
      if (TradeId.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(TradeId);
      }
      if (OtherPartyUuid.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(OtherPartyUuid);
      }
      if (FailureReason != 0) {
        size += 1 + pb::CodedOutputStream.ComputeEnumSize((int) FailureReason);
      }
      if (FailureMessage.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(FailureMessage);
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(CreateTradeResponsePacket other) {
      if (other == null) {
        return;
      }
      if (other.Success != false) {
        Success = other.Success;
      }
      if (other.TradeId.Length != 0) {
        TradeId = other.TradeId;
      }
      if (other.OtherPartyUuid.Length != 0) {
        OtherPartyUuid = other.OtherPartyUuid;
      }
      if (other.FailureReason != 0) {
        FailureReason = other.FailureReason;
      }
      if (other.FailureMessage.Length != 0) {
        FailureMessage = other.FailureMessage;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(pb::CodedInputStream input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            input.SkipLastField();
            break;
          case 8: {
            Success = input.ReadBool();
            break;
          }
          case 18: {
            TradeId = input.ReadString();
            break;
          }
          case 26: {
            OtherPartyUuid = input.ReadString();
            break;
          }
          case 32: {
            failureReason_ = (global::Trading.TradeFailureReason) input.ReadEnum();
            break;
          }
          case 42: {
            FailureMessage = input.ReadString();
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
