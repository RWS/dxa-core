using System;
using System.Collections.Generic;

namespace Tridion.Dxa.Framework.ADF.ClaimStore
{
    /// <summary>
    /// IClaimStore
    /// 
    /// Ambient Data Framework ClaimStore interface
    /// </summary>
    public interface IClaimStore
    {
        object Get(Uri claimUri);

        T Get<T>(Uri claimUri);

        T Get<T>(string claimUri);

        IDictionary<Uri, object> GetAll();

        ISet<Uri> GetAllReadOnlyClaims();

        ISet<Uri> GetAllImmutableClaims();

        void Put(Uri claimUri, object value);

        void Put(Uri claimUri, Object value, bool writeOnce);

        void Put(Uri claimUri, Object value, ClaimType claimType);

        void Put(Uri claimUri, Object value, ClaimValueScope? claimScope);

        void Put(Uri claimUri, Object value, ClaimType claimType, ClaimValueScope? claimScope);

        void Remove(Uri claimUri);

        bool Contains(Uri claimUri);

        void ClearReadOnly();

        void ClearImmutable();

        bool IsReadOnly(Uri claimUri);

        bool IsImmutable(Uri claimUri);

        void SetReadOnlyClaims(ISet<Uri> readOnlyClaims);

        void SetImmutableClaims(ISet<Uri> immutableClaims);

        void RemoveRequestScopedClaims();

        void RemoveStaticScopedClaims();

        IDictionary<Uri, object> ClaimValues { get; }

        ISet<Uri> ReadOnly { get; }

        ISet<Uri> Immutable { get; }

        IDictionary<Uri, ClaimValueScope> AdHocClaimScopes { get; }

        IClaimStore Clone();

        string SeraializeJson();

        bool Update(string json);
    }
}
