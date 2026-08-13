using NTwain;
using NTwain.Data;

namespace MtToolkit
{
    class MtCapabilities
    {
        readonly IDataSource m_DataSource;

        public MtCapabilities(IDataSource dataSource)
        {
            m_DataSource = dataSource;
        }

        ICapWrapper<int> m_Density;

        public ICapWrapper<int> Density
        {
            get
            {
                if (m_Density == null)
                {
                    m_Density = new CapWrapper<int>(m_DataSource, CapabilityId.CustomBase + 1102, ValueExtensions.ConvertToEnum<int>,
                        value => new TWOneValue
                        {
                            Item = (uint)value,
                            ItemType = ItemType.UInt16
                        });
                }
                return m_Density;
            }
        }

        ICapWrapper<float> m_DensityRange;

        public ICapWrapper<float> DensityRange
        {
            get
            {
                if (m_DensityRange == null)
                {
                    m_DensityRange = new CapWrapper<float>(m_DataSource, CapabilityId.CustomBase + 1103, ValueExtensions.ConvertToEnum<float>, true);
                }
                return m_DensityRange;
            }
        }

        ICapWrapper<BoolType> m_AutoCrop;

        public ICapWrapper<BoolType> AutoCrop
        {
            get
            {
                if (m_AutoCrop == null)
                {
                    m_AutoCrop = new CapWrapper<BoolType>(m_DataSource, CapabilityId.CustomBase + 1106, ValueExtensions.ConvertToEnum<BoolType>,
                        value => new TWOneValue
                        {
                            Item = (uint)value,
                            ItemType = ItemType.UInt16
                        });
                }
                return m_AutoCrop;
            }
        }

        ICapWrapper<BoolType> m_AutoLevel;

        public ICapWrapper<BoolType> AutoLevel
        {
            get
            {
                if (m_AutoLevel == null)
                {
                    m_AutoLevel = new CapWrapper<BoolType>(m_DataSource, CapabilityId.CustomBase + 1107, ValueExtensions.ConvertToEnum<BoolType>,
                        value => new TWOneValue
                        {
                            Item = (uint)value,
                            ItemType = ItemType.UInt16
                        });
                }
                return m_AutoLevel;
            }
        }

        ICapWrapper<BoolType> m_AutoFilmFeeder;

        public ICapWrapper<BoolType> AutoFilmFeeder
        {
            get
            {
                if (m_AutoFilmFeeder == null)
                {
                    m_AutoFilmFeeder = new CapWrapper<BoolType>(m_DataSource, CapabilityId.CustomBase + 1108, ValueExtensions.ConvertToEnum<BoolType>,
                        value => new TWOneValue
                        {
                            Item = (uint)value,
                            ItemType = ItemType.UInt16
                        });
                }
                return m_AutoFilmFeeder;
            }
        }

        ICapWrapper<BoolType> m_MultiChannelCrop;

        public ICapWrapper<BoolType> MultiChannelCrop
        {
            get
            {
                if (m_MultiChannelCrop == null)
                {
                    m_MultiChannelCrop = new CapWrapper<BoolType>(m_DataSource, CapabilityId.CustomBase + 1109, ValueExtensions.ConvertToEnum<BoolType>,
                        value => new TWOneValue
                        {
                            Item = (uint)value,
                            ItemType = ItemType.UInt16
                        });
                }
                return m_MultiChannelCrop;
            }
        }
    }
}
