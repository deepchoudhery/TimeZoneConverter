//------------------------------------------------------------------------------
// Hand-maintained wire types for ITimeZoneConverter (split out of the former
// SvcUtil-generated Contracts/ITimeZoneConverter.cs during the CoreWCF migration).
//
// XmlType/XmlElement namespaces and Order values, and the MessageContract
// WrapperName/WrapperNamespace/IsWrapped and MessageBodyMember Namespace/Order
// values, define the SOAP wire format and must stay identical to the original.
// The generated client proxy (TimeZoneConverterClient / ITimeZoneConverterChannel)
// was unused and has been removed from the server.
//------------------------------------------------------------------------------

namespace TimeZoneConverter.Contracts
{
    
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://bartjolling.github.io/")]
    public partial class TimeZoneConversionRequest
    {
        
        private System.DateTime fromDateTimeField;
        
        private int fromOffsetField;
        
        private int toOffsetField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=0)]
        public System.DateTime FromDateTime
        {
            get
            {
                return this.fromDateTimeField;
            }
            set
            {
                this.fromDateTimeField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=1)]
        public int FromOffset
        {
            get
            {
                return this.fromOffsetField;
            }
            set
            {
                this.fromOffsetField = value;
            }
        }
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=2)]
        public int ToOffset
        {
            get
            {
                return this.toOffsetField;
            }
            set
            {
                this.toOffsetField = value;
            }
        }
    }
    
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace="http://bartjolling.github.io/")]
    public partial class TimeZoneConversionResponse
    {
        
        private System.DateTime toDateTimeField;
        
        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Order=0)]
        public System.DateTime ToDateTime
        {
            get
            {
                return this.toDateTimeField;
            }
            set
            {
                this.toDateTimeField = value;
            }
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [CoreWCF.MessageContractAttribute(WrapperName="ConvertToOffsetRequest", WrapperNamespace="http://bartjolling.github.io/", IsWrapped=true)]
    public partial class ConvertToOffsetRequest
    {
        
        [CoreWCF.MessageBodyMemberAttribute(Namespace="http://bartjolling.github.io/", Order=0)]
        public TimeZoneConverter.Contracts.TimeZoneConversionRequest TimeZoneConversionRequest;
        
        public ConvertToOffsetRequest()
        {
        }
        
        public ConvertToOffsetRequest(TimeZoneConverter.Contracts.TimeZoneConversionRequest TimeZoneConversionRequest)
        {
            this.TimeZoneConversionRequest = TimeZoneConversionRequest;
        }
    }
    
    [System.Diagnostics.DebuggerStepThroughAttribute()]
    [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
    [CoreWCF.MessageContractAttribute(WrapperName="ConvertToOffsetResponse", WrapperNamespace="http://bartjolling.github.io/", IsWrapped=true)]
    public partial class ConvertToOffsetResponse
    {
        
        [CoreWCF.MessageBodyMemberAttribute(Namespace="http://bartjolling.github.io/", Order=0)]
        public TimeZoneConverter.Contracts.TimeZoneConversionResponse TimeZoneConversionResponse;
        
        public ConvertToOffsetResponse()
        {
        }
        
        public ConvertToOffsetResponse(TimeZoneConverter.Contracts.TimeZoneConversionResponse TimeZoneConversionResponse)
        {
            this.TimeZoneConversionResponse = TimeZoneConversionResponse;
        }
    }
}