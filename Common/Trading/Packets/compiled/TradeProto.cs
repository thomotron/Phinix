// Generated by the protocol buffer compiler.  DO NOT EDIT!
// source: Packets/TradeProto.proto
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Trading {

  /// <summary>Holder for reflection information generated from Packets/TradeProto.proto</summary>
  public static partial class TradeProtoReflection {

    #region Descriptor
    /// <summary>File descriptor for Packets/TradeProto.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static TradeProtoReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "ChhQYWNrZXRzL1RyYWRlUHJvdG8ucHJvdG8SB1RyYWRpbmcaGFBhY2tldHMv",
            "UHJvdG9UaGluZy5wcm90byK1AQoKVHJhZGVQcm90bxIPCgdUcmFkZUlkGAEg",
            "ASgJEhYKDk90aGVyUGFydHlVdWlkGAIgASgJEiIKBUl0ZW1zGAMgAygLMhMu",
            "VHJhZGluZy5Qcm90b1RoaW5nEiwKD090aGVyUGFydHlJdGVtcxgEIAMoCzIT",
            "LlRyYWRpbmcuUHJvdG9UaGluZxIQCghBY2NlcHRlZBgFIAEoCBIaChJPdGhl",
            "clBhcnR5QWNjZXB0ZWQYBiABKAhiBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Trading.ProtoThingReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Trading.TradeProto), global::Trading.TradeProto.Parser, new[]{ "TradeId", "OtherPartyUuid", "Items", "OtherPartyItems", "Accepted", "OtherPartyAccepted" }, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class TradeProto : pb::IMessage<TradeProto> {
    private static readonly pb::MessageParser<TradeProto> _parser = new pb::MessageParser<TradeProto>(() => new TradeProto());
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<TradeProto> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Trading.TradeProtoReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public TradeProto() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public TradeProto(TradeProto other) : this() {
      tradeId_ = other.tradeId_;
      otherPartyUuid_ = other.otherPartyUuid_;
      items_ = other.items_.Clone();
      otherPartyItems_ = other.otherPartyItems_.Clone();
      accepted_ = other.accepted_;
      otherPartyAccepted_ = other.otherPartyAccepted_;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public TradeProto Clone() {
      return new TradeProto(this);
    }

    /// <summary>Field number for the "TradeId" field.</summary>
    public const int TradeIdFieldNumber = 1;
    private string tradeId_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string TradeId {
      get { return tradeId_; }
      set {
        tradeId_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "OtherPartyUuid" field.</summary>
    public const int OtherPartyUuidFieldNumber = 2;
    private string otherPartyUuid_ = "";
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public string OtherPartyUuid {
      get { return otherPartyUuid_; }
      set {
        otherPartyUuid_ = pb::ProtoPreconditions.CheckNotNull(value, "value");
      }
    }

    /// <summary>Field number for the "Items" field.</summary>
    public const int ItemsFieldNumber = 3;
    private static readonly pb::FieldCodec<global::Trading.ProtoThing> _repeated_items_codec
        = pb::FieldCodec.ForMessage(26, global::Trading.ProtoThing.Parser);
    private readonly pbc::RepeatedField<global::Trading.ProtoThing> items_ = new pbc::RepeatedField<global::Trading.ProtoThing>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::Trading.ProtoThing> Items {
      get { return items_; }
    }

    /// <summary>Field number for the "OtherPartyItems" field.</summary>
    public const int OtherPartyItemsFieldNumber = 4;
    private static readonly pb::FieldCodec<global::Trading.ProtoThing> _repeated_otherPartyItems_codec
        = pb::FieldCodec.ForMessage(34, global::Trading.ProtoThing.Parser);
    private readonly pbc::RepeatedField<global::Trading.ProtoThing> otherPartyItems_ = new pbc::RepeatedField<global::Trading.ProtoThing>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::Trading.ProtoThing> OtherPartyItems {
      get { return otherPartyItems_; }
    }

    /// <summary>Field number for the "Accepted" field.</summary>
    public const int AcceptedFieldNumber = 5;
    private bool accepted_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Accepted {
      get { return accepted_; }
      set {
        accepted_ = value;
      }
    }

    /// <summary>Field number for the "OtherPartyAccepted" field.</summary>
    public const int OtherPartyAcceptedFieldNumber = 6;
    private bool otherPartyAccepted_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool OtherPartyAccepted {
      get { return otherPartyAccepted_; }
      set {
        otherPartyAccepted_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as TradeProto);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(TradeProto other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (TradeId != other.TradeId) return false;
      if (OtherPartyUuid != other.OtherPartyUuid) return false;
      if(!items_.Equals(other.items_)) return false;
      if(!otherPartyItems_.Equals(other.otherPartyItems_)) return false;
      if (Accepted != other.Accepted) return false;
      if (OtherPartyAccepted != other.OtherPartyAccepted) return false;
      return true;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (TradeId.Length != 0) hash ^= TradeId.GetHashCode();
      if (OtherPartyUuid.Length != 0) hash ^= OtherPartyUuid.GetHashCode();
      hash ^= items_.GetHashCode();
      hash ^= otherPartyItems_.GetHashCode();
      if (Accepted != false) hash ^= Accepted.GetHashCode();
      if (OtherPartyAccepted != false) hash ^= OtherPartyAccepted.GetHashCode();
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void WriteTo(pb::CodedOutputStream output) {
      if (TradeId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(TradeId);
      }
      if (OtherPartyUuid.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(OtherPartyUuid);
      }
      items_.WriteTo(output, _repeated_items_codec);
      otherPartyItems_.WriteTo(output, _repeated_otherPartyItems_codec);
      if (Accepted != false) {
        output.WriteRawTag(40);
        output.WriteBool(Accepted);
      }
      if (OtherPartyAccepted != false) {
        output.WriteRawTag(48);
        output.WriteBool(OtherPartyAccepted);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (TradeId.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(TradeId);
      }
      if (OtherPartyUuid.Length != 0) {
        size += 1 + pb::CodedOutputStream.ComputeStringSize(OtherPartyUuid);
      }
      size += items_.CalculateSize(_repeated_items_codec);
      size += otherPartyItems_.CalculateSize(_repeated_otherPartyItems_codec);
      if (Accepted != false) {
        size += 1 + 1;
      }
      if (OtherPartyAccepted != false) {
        size += 1 + 1;
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(TradeProto other) {
      if (other == null) {
        return;
      }
      if (other.TradeId.Length != 0) {
        TradeId = other.TradeId;
      }
      if (other.OtherPartyUuid.Length != 0) {
        OtherPartyUuid = other.OtherPartyUuid;
      }
      items_.Add(other.items_);
      otherPartyItems_.Add(other.otherPartyItems_);
      if (other.Accepted != false) {
        Accepted = other.Accepted;
      }
      if (other.OtherPartyAccepted != false) {
        OtherPartyAccepted = other.OtherPartyAccepted;
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
          case 10: {
            TradeId = input.ReadString();
            break;
          }
          case 18: {
            OtherPartyUuid = input.ReadString();
            break;
          }
          case 26: {
            items_.AddEntriesFrom(input, _repeated_items_codec);
            break;
          }
          case 34: {
            otherPartyItems_.AddEntriesFrom(input, _repeated_otherPartyItems_codec);
            break;
          }
          case 40: {
            Accepted = input.ReadBool();
            break;
          }
          case 48: {
            OtherPartyAccepted = input.ReadBool();
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
