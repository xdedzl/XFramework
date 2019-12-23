namespace XFramework
{
    /// <summary>
    /// 进度
    /// </summary>
    public interface IProgress
    {
        bool IsDone { get; }
        float Progress { get; }
    }

    public class DefaultProgress : IProgress
    {
        public bool IsDone => true;

        public float Progress => 1;
    }

    public class MultiProgress : IProgress
    {
        private IProgress[] progresses;

        public MultiProgress(IProgress[] progresses)
        {
            this.progresses = progresses;
        }

        public bool IsDone
        {
            get
            {
                foreach (var item in progresses)
                {
                    if (!item.IsDone)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public float Progress
        {
            get
            {
                float p = 0;
                foreach (var item in progresses)
                {
                    p += item.Progress;
                }
                return p / progresses.Length;
            }
        }
    }

    public class DynamicMultiProgress : IProgress
    {
        private int index = 0;
        private IProgress[] progresses;
        private float[] ratios;

        public DynamicMultiProgress(int count, params float[] ratios)
        {
            if (ratios == null || count != ratios.Length)
            {
                throw new FrameworkException("[DynamicMultiProgress] 需传入正确的参数");
            }

            float plus = 0;
            foreach (var item in ratios)
            {
                plus += item;
            }
            if (plus != 1)
            {
                throw new FrameworkException("[DynamicMultiProgress] 需传入争取的比例");
            }
            progresses = new IProgress[count];
            this.ratios = ratios;
        }

        public void Add(IProgress progress)
        {
            progresses[index] = progress;
            index++;
        }

        public bool IsDone
        {
            get
            {
                foreach (var item in progresses)
                {
                    if (item == null || !item.IsDone)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public float Progress
        {
            get
            {
                float p = 0;
                for (int i = 0; i < progresses.Length; i++)
                {
                    if (progresses[i] != null)
                    {
                        p += progresses[i].Progress * ratios[i];
                    }
                }

                return p;
            }
        }
    }
}