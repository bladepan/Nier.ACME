using System;
using System.Text.Json;
using ACMESharp.Protocol.Resources;
using ACMESharpOrderDetails = ACMESharp.Protocol.OrderDetails;
using ACMESharpOrder = ACMESharp.Protocol.Resources.Order;

namespace Nier.ACME.Core
{
    public enum OrderStatus
    {
        None,
        Pending,
        Ready,
        Processing,
        Valid,
        Invalid
    }

    public class OrderDetails
    {
        public OrderStatus Status { get; set; }
        public long Expires { get; set; }
        public string Url { get; set; }
        public string FinalizeUrl { get; set; }
        public string CertificateUrl { get; set; }
        public string[] AuthorizationUrls { get; set; }

        public Problem Error { get; }

        public OrderDetails()
        {
        }

        public OrderDetails(ACMESharpOrderDetails orderDetails)
        {
            ACMESharpOrder order = orderDetails.Payload;


            Status = Enum.Parse<OrderStatus>(order.Status, true);

            if (!string.IsNullOrEmpty(orderDetails.Payload.Expires))
            {
                if (DateTimeOffset.TryParse(orderDetails.Payload.Expires, out DateTimeOffset dateTimeOffset))
                {
                    Expires = dateTimeOffset.ToUnixTimeMilliseconds();
                }
                else
                {
                    throw new ArgumentException($"Invalid expire time {orderDetails.Payload.Expires}");
                }
            }

            Url = orderDetails.OrderUrl;
            FinalizeUrl = orderDetails.Payload.Finalize;
            CertificateUrl = orderDetails.Payload.Certificate;
            AuthorizationUrls = orderDetails.Payload.Authorizations;
            Error = orderDetails.Payload.Error;
        }

        public bool IsExpired(long now)
        {
            return now >= Expires;
        }

        public void AssertOkResponse()
        {
            if (Error != null)
            {
                throw new ACMEException($"Error creating ACME order {JsonSerializer.Serialize(Error)}");
            }

            if (Status == OrderStatus.Invalid)
            {
                throw new InvalidOrderStatusException("Order state is invalid");
            }
        }

        public void Merge(OrderDetails another)
        {
            if (Status == OrderStatus.None)
            {
                Status = another.Status;
            }

            if (string.IsNullOrEmpty(Url))
            {
                Url = another.Url;
            }

            if (string.IsNullOrEmpty(FinalizeUrl))
            {
                FinalizeUrl = another.FinalizeUrl;
            }

            if (string.IsNullOrEmpty(CertificateUrl))
            {
                CertificateUrl = another.CertificateUrl;
            }

            if (Expires == 0)
            {
                Expires = another.Expires;
            }

            if (AuthorizationUrls == null)
            {
                AuthorizationUrls = another.AuthorizationUrls;
            }
        }
    }
}