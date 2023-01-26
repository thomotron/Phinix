// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: Packets/CompleteTradePacket.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Trading {

  /// <summary>Holder for reflection information generated from Packets/CompleteTradePacket.proto</summary>
  public static partial class CompleteTradePacketReflection {

    #region Descriptor
    /// <summary>File descriptor for Packets/CompleteTradePacket.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static CompleteTradePacketReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CiFQYWNrZXRzL0NvbXBsZXRlVHJhZGVQYWNrZXQucHJvdG8SB1RyYWRpbmca",
            "ElBhY2tldHMvUGF3bi5wcm90bxoTUGFja2V0cy9UaGluZy5wcm90byKWAQoT",
            "Q29tcGxldGVUcmFkZVBhY2tldBIPCgdUcmFkZUlkGAEgASgJEhYKDk90aGVy",
            "UGFydHlVdWlkGAIgASgJEg8KB1N1Y2Nlc3MYAyABKAgSIgoFSXRlbXMYBCAD",
            "KAsyEy5UcmFkaW5nLlByb3RvVGhpbmcSIQoFUGF3bnMYBSADKAsyEi5UcmFk",
            "aW5nLlByb3RvUGF3bmIGcHJvdG8z"));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Trading.PawnReflection.Descriptor, global::Trading.ThingReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Trading.CompleteTradePacket), global::Trading.CompleteTradePacket.Parser, new[]{ "TradeId", "OtherPartyUuid", "Success", "Items", "Pawns" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class CompleteTradePacket : pb::IMessage<CompleteTradePacket> {
    private static readonly pb::MessageParser<CompleteTradePacket> _parser = new pb::MessageParser<CompleteTradePacket>(() => new CompleteTradePacket());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<CompleteTradePacket> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Trading.CompleteTradePacketReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CompleteTradePacket() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CompleteTradePacket(CompleteTradePacket other) : this() {
      tradeId_ = other.tradeId_;
      otherPartyUuid_ = other.otherPartyUuid_;
      success_ = other.success_;
      items_ = other.items_.Clone();
      pawns_ = other.pawns_.Clone();
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public CompleteTradePacket Clone() {
      return new CompleteTradePacket(this);
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

    /// <summary>Field number for the "Success" field.</summary>
    public const int SuccessFieldNumber = 3;
    private bool success_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Success {
      get { return success_; }
      set {
        success_ = value;
      }
    }

    /// <summary>Field number for the "Items" field.</summary>
    public const int ItemsFieldNumber = 4;
    private static readonly pb::FieldCodec<global::Trading.ProtoThing> _repeated_items_codec
        = pb::FieldCodec.ForMessage(34, global::Trading.ProtoThing.Parser);
    private readonly pbc::RepeatedField<global::Trading.ProtoThing> items_ = new pbc::RepeatedField<global::Trading.ProtoThing>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::Trading.ProtoThing> Items {
      get { return items_; }
    }

    /// <summary>Field number for the "Pawns" field.</summary>
    public const int PawnsFieldNumber = 5;
    private static readonly pb::FieldCodec<global::Trading.ProtoPawn> _repeated_pawns_codec
        = pb::FieldCodec.ForMessage(42, global::Trading.ProtoPawn.Parser);
    private readonly pbc::RepeatedField<global::Trading.ProtoPawn> pawns_ = new pbc::RepeatedField<global::Trading.ProtoPawn>();
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public pbc::RepeatedField<global::Trading.ProtoPawn> Pawns {
      get { return pawns_; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as CompleteTradePacket);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(CompleteTradePacket other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (TradeId != other.TradeId) return false;
      if (OtherPartyUuid != other.OtherPartyUuid) return false;
      if (Success != other.Success) return false;
      if(!items_.Equals(other.items_)) return false;
      if(!pawns_.Equals(other.pawns_)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (TradeId.Length != 0) hash ^= TradeId.GetHashCode();
      if (OtherPartyUuid.Length != 0) hash ^= OtherPartyUuid.GetHashCode();
      if (Success != false) hash ^= Success.GetHashCode();
      hash ^= items_.GetHashCode();
      hash ^= pawns_.GetHashCode();
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
      if (TradeId.Length != 0) {
        output.WriteRawTag(10);
        output.WriteString(TradeId);
      }
      if (OtherPartyUuid.Length != 0) {
        output.WriteRawTag(18);
        output.WriteString(OtherPartyUuid);
      }
      if (Success != false) {
        output.WriteRawTag(24);
        output.WriteBool(Success);
      }
      items_.WriteTo(output, _repeated_items_codec);
      pawns_.WriteTo(output, _repeated_pawns_codec);
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
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
      if (Success != false) {
        size += 1 + 1;
      }
      size += items_.CalculateSize(_repeated_items_codec);
      size += pawns_.CalculateSize(_repeated_pawns_codec);
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(CompleteTradePacket other) {
      if (other == null) {
        return;
      }
      if (other.TradeId.Length != 0) {
        TradeId = other.TradeId;
      }
      if (other.OtherPartyUuid.Length != 0) {
        OtherPartyUuid = other.OtherPartyUuid;
      }
      if (other.Success != false) {
        Success = other.Success;
      }
      items_.Add(other.items_);
      pawns_.Add(other.pawns_);
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
            TradeId = input.ReadString();
            break;
          }
          case 18: {
            OtherPartyUuid = input.ReadString();
            break;
          }
          case 24: {
            Success = input.ReadBool();
            break;
          }
          case 34: {
            items_.AddEntriesFrom(input, _repeated_items_codec);
            break;
          }
          case 42: {
            pawns_.AddEntriesFrom(input, _repeated_pawns_codec);
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
