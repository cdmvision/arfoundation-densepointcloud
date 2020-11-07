using System.Collections.Generic;

namespace Cdm.XR.Extensions
{
    public readonly struct ARDensePointCloudsChangedEventArgs 
    {
        /// <summary>
        /// The list of <see cref="ARDensePointCloud"/>s added since the last event.
        /// </summary>
        public IReadOnlyList<ARDensePointCloud> added { get; }

        /// <summary>
        /// The list of <see cref="ARDensePointCloud"/>s udpated since the last event.
        /// </summary>
        public IReadOnlyList<ARDensePointCloud> updated { get; }

        /// <summary>
        /// The list of <see cref="ARDensePointCloud"/>s removed since the last event.
        /// </summary>
        public IReadOnlyList<ARDensePointCloud> removed { get; }
        
        /// <summary>
        /// Constructs an <see cref="ARDensePointCloudsChangedEventArgs"/>.
        /// </summary>
        /// <param name="added">The list of <see cref="ARDensePointCloud"/>s added since the last event.</param>
        /// <param name="updated">The list of <see cref="ARDensePointCloud"/>s updated since the last event.</param>
        /// <param name="removed">The list of <see cref="ARDensePointCloud"/>s removed since the last event.</param>
        public ARDensePointCloudsChangedEventArgs(
            IReadOnlyList<ARDensePointCloud> added,
            IReadOnlyList<ARDensePointCloud> updated,
            IReadOnlyList<ARDensePointCloud> removed)
        {
            this.added = added;
            this.updated = updated;
            this.removed = removed;
        }
    }
}