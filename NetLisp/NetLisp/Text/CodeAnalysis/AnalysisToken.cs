using NetLisp.Data;
using NetLisp.Runtime;
using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Text.CodeAnalysis
{
    public class AnalysisToken : ExtendedLispToken
    {
        public static ExtendedTypeInfo AnalysisTokenExtendedTypeInfo { get; } = new ExtendedTypeInfo()
        {
            ExtendedTypeName = "",
            ExtendedTypeGuid = new Guid("2d43d67e-e10f-40b5-a6b9-5202f023876e")
        };

        public override ExtendedTypeInfo ExtendedTypeInfo => AnalysisTokenExtendedTypeInfo;

        public AnalysisToken(MetadataTypeRestrictions tokenTypeRestriction)
        {
            TokenTypeRestriction = tokenTypeRestriction;
        }
        public AnalysisToken(LispToken? simpleValue)
        {
            if (simpleValue == null)
            {
                TokenTypeRestriction = MetadataTypeRestrictions.NoType;
            } else
            {
                TokenTypeRestriction = new MetadataTypeRestrictions(new LispTypeInfo[] { simpleValue.GetTypeInfo() });
            }
        }
        public AnalysisToken(params LispTypeInfo[] types)
        {
            TokenTypeRestriction = new MetadataTypeRestrictions(types);
        }
        public AnalysisToken(LispFunction functionDefinition)
        {
            PotentialExecutableDefinition = functionDefinition;
            TokenTypeRestriction = new MetadataTypeRestrictions(new LispTypeInfo[] { LispTypeInfo.FromSimpleType(LispDataType.Function) });
        }

        public MetadataTypeRestrictions TokenTypeRestriction { get; set; }
        public ExecutableLispToken? PotentialExecutableDefinition { get; set; } = null;

        public void MergeAssign(LispToken? token)
        {
            if (token == null)
            {
                token = new AnalysisToken(MetadataTypeRestrictions.AnyType);
            }
            LispTypeInfo tokenType = token.GetTypeInfo();
            if (tokenType == AnalysisTokenExtendedTypeInfo)
            {
                AnalysisToken mergeAnalysisToken = (AnalysisToken)token;
                TokenTypeRestriction = MetadataTypeRestrictions.Merge(TokenTypeRestriction, mergeAnalysisToken.TokenTypeRestriction);
                if (mergeAnalysisToken.PotentialExecutableDefinition != null)
                {
                    mergeFunctionMetadata(mergeAnalysisToken.PotentialExecutableDefinition);
                }
            } else
            {
                TokenTypeRestriction.Include(tokenType);
                if (token.TypeCanBeExecuted)
                {
                    mergeFunctionMetadata((ExecutableLispToken)token);
                }
            }
        }

        private void mergeFunctionMetadata(ExecutableLispToken assignToken)
        {
            ExecutableTokenMetadata assignMetadata = assignToken.Metadata;
            ExecutableTokenMetadata? currentMetadata = PotentialExecutableDefinition?.Metadata ?? null;
            // logic description: if current has no metadata or non argument defined metadata, use the newly
            // assigned metadata. if the current metadata has defined argument info and the newly assigned
            // does not, use the current metadata. if they are both argument defined, use, the new metadata.
            if (currentMetadata == null)
            {
                PotentialExecutableDefinition = assignToken;
            } else
            {
                if (assignMetadata.HasDefinedArguments)
                {
                    PotentialExecutableDefinition = assignToken;
                } else if (!currentMetadata.HasDefinedArguments)
                {
                    PotentialExecutableDefinition = assignToken;
                }
            }
        }

        public override bool CompareValue(LispToken token)
        {
            throw new InvalidOperationException();
        }

        public override int HashValue()
        {
            throw new InvalidOperationException();
        }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            throw new InvalidOperationException();
        }
    }
}
