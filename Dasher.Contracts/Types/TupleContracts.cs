using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dasher.Contracts.Utils;

namespace Dasher.Contracts.Types
{
    internal sealed class TupleReadContract : ByValueContract, IReadContract
    {
        public static bool CanProcess(Type type) => TupleWriteContract.CanProcess(type);

        private IReadOnlyList<IReadContract> Items { get; }

        public TupleReadContract(Type type, ContractCollection contractCollection)
        {
            if (!TupleWriteContract.CanProcess(type))
                throw new ArgumentException($"Type {type} is not a supported tuple type.", nameof(type));

            Items = type.GetGenericArguments().Select(contractCollection.GetOrAddReadContract).ToList();
        }

        public TupleReadContract(IReadOnlyList<IReadContract> items)
        {
            Items = items;
        }

        public bool CanReadFrom(IWriteContract writeContract, bool strict)
        {
            var that = writeContract as TupleWriteContract;

            return that?.Items.Count == Items.Count
                   && !Items.Where((rs, i) => !rs.CanReadFrom(that.Items[i], strict)).Any();
        }

        public override bool Equals(Contract other) => (other as TupleReadContract)?.Items.SequenceEqual(Items) ?? false;

        protected override int ComputeHashCode()
        {
            unchecked
            {
                var hash = 0;
                foreach (var item in Items)
                {
                    hash <<= 5;
                    hash ^= item.GetHashCode();
                }
                return hash;
            }
        }

        internal override IEnumerable<Contract> Children => Items.Cast<Contract>();

        internal override string MarkupValue
        {
            get { return $"{{tuple {string.Join(" ", Items.Select(i => i.ToReferenceString()))}}}"; }
        }

        public IReadContract CopyTo(ContractCollection collection)
        {
            return collection.GetOrCreate(this, () => new TupleReadContract(Items.Select(i => i.CopyTo(collection)).ToList()));
        }
    }

    internal sealed class TupleWriteContract : ByValueContract, IWriteContract
    {
        public static bool CanProcess(Type type)
        {
            if (!type.GetTypeInfo().IsGenericType)
                return false;
            if (!type.IsConstructedGenericType)
                return false;

            var genType = type.GetGenericTypeDefinition();

            return genType == typeof(Tuple<>) ||
                   genType == typeof(Tuple<,>) ||
                   genType == typeof(Tuple<,,>) ||
                   genType == typeof(Tuple<,,,>) ||
                   genType == typeof(Tuple<,,,,>) ||
                   genType == typeof(Tuple<,,,,,>) ||
                   genType == typeof(Tuple<,,,,,,>) ||
                   genType == typeof(Tuple<,,,,,,,>) ||
                   genType == typeof(Tuple<,,,,,,,>);
        }

        public IReadOnlyList<IWriteContract> Items { get; }

        public TupleWriteContract(Type type, ContractCollection contractCollection)
        {
            if (!CanProcess(type))
                throw new ArgumentException($"Type {type} is not a supported tuple type.", nameof(type));

            Items = type.GetGenericArguments().Select(contractCollection.GetOrAddWriteContract).ToList();
        }

        public TupleWriteContract(IReadOnlyList<IWriteContract> items)
        {
            Items = items;
        }

        public override bool Equals(Contract other) => (other as TupleWriteContract)?.Items.SequenceEqual(Items) ?? false;

        protected override int ComputeHashCode()
        {
            unchecked
            {
                var hash = 0;
                foreach (var item in Items)
                {
                    hash <<= 5;
                    hash ^= item.GetHashCode();
                }
                return hash;
            }
        }

        internal override IEnumerable<Contract> Children => Items.Cast<Contract>();

        internal override string MarkupValue
        {
            get { return $"{{tuple {string.Join(" ", Items.Select(i => i.ToReferenceString()))}}}"; }
        }

        public IWriteContract CopyTo(ContractCollection collection)
        {
            return collection.GetOrCreate(this, () => new TupleWriteContract(Items.Select(i => i.CopyTo(collection)).ToList()));
        }
    }
}