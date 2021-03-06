// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: Packets/ExtendSessionResponsePacket.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Authentication {

  /// <summary>Holder for reflection information generated from Packets/ExtendSessionResponsePacket.proto</summary>
  public static partial class ExtendSessionResponsePacketReflection {

    #region Descriptor
    /// <summary>File descriptor for Packets/ExtendSessionResponsePacket.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static ExtendSessionResponsePacketReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CilQYWNrZXRzL0V4dGVuZFNlc3Npb25SZXNwb25zZVBhY2tldC5wcm90bxIO",
            "QXV0aGVudGljYXRpb24iUgobRXh0ZW5kU2Vzc2lvblJlc3BvbnNlUGFja2V0",
            "Eg8KB1N1Y2Nlc3MYASABKAgSEQoJRXhwaXJlc0luGAMgASgFSgQIAhADUglO",
            "ZXdFeHBpcnliBnByb3RvMw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Authentication.ExtendSessionResponsePacket), global::Authentication.ExtendSessionResponsePacket.Parser, new[]{ "Success", "ExpiresIn" }, null, null, null, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class ExtendSessionResponsePacket : pb::IMessage<ExtendSessionResponsePacket> {
    private static readonly pb::MessageParser<ExtendSessionResponsePacket> _parser = new pb::MessageParser<ExtendSessionResponsePacket>(() => new ExtendSessionResponsePacket());
    private pb::UnknownFieldSet _unknownFields;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pb::MessageParser<ExtendSessionResponsePacket> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Authentication.ExtendSessionResponsePacketReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ExtendSessionResponsePacket() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ExtendSessionResponsePacket(ExtendSessionResponsePacket other) : this() {
      success_ = other.success_;
      expiresIn_ = other.expiresIn_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public ExtendSessionResponsePacket Clone() {
      return new ExtendSessionResponsePacket(this);
    }

    /// <summary>Field number for the "Success" field.</summary>
    public const int SuccessFieldNumber = 1;
    private bool success_;
    /// <summary>
    /// Whether the session extension was successful
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Success {
      get { return success_; }
      set {
        success_ = value;
      }
    }

    /// <summary>Field number for the "ExpiresIn" field.</summary>
    public const int ExpiresInFieldNumber = 3;
    private int expiresIn_;
    /// <summary>
    /// Milliseconds until the next expiry
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int ExpiresIn {
      get { return expiresIn_; }
      set {
        expiresIn_ = value;
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override bool Equals(object other) {
      return Equals(other as ExtendSessionResponsePacket);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public bool Equals(ExtendSessionResponsePacket other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (Success != other.Success) return false;
      if (ExpiresIn != other.ExpiresIn) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public override int GetHashCode() {
      int hash = 1;
      if (Success != false) hash ^= Success.GetHashCode();
      if (ExpiresIn != 0) hash ^= ExpiresIn.GetHashCode();
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
      if (Success != false) {
        output.WriteRawTag(8);
        output.WriteBool(Success);
      }
      if (ExpiresIn != 0) {
        output.WriteRawTag(24);
        output.WriteInt32(ExpiresIn);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public int CalculateSize() {
      int size = 0;
      if (Success != false) {
        size += 1 + 1;
      }
      if (ExpiresIn != 0) {
        size += 1 + pb::CodedOutputStream.ComputeInt32Size(ExpiresIn);
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    public void MergeFrom(ExtendSessionResponsePacket other) {
      if (other == null) {
        return;
      }
      if (other.Success != false) {
        Success = other.Success;
      }
      if (other.ExpiresIn != 0) {
        ExpiresIn = other.ExpiresIn;
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
          case 8: {
            Success = input.ReadBool();
            break;
          }
          case 24: {
            ExpiresIn = input.ReadInt32();
            break;
          }
        }
      }
    }

  }

  #endregion

}

#endregion Designer generated code
