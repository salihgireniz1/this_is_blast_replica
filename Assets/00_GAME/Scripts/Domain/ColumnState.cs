// ColumnState - Domain.
// What targeting may do with a board column while Presentation is still showing the
// last shot at it. The domain removes a cube the instant it is shot, so without this the
// cube behind would be a target before the bullet has even landed. Responsible for naming
// the three states; the loop that sets them and the rules that read them live elsewhere.
// Not responsible for timing - when a column moves between states is Presentation's call.

namespace Blast.Domain
{
    /// <summary>How far a column's last shot has played out on screen, for targeting.</summary>
    public enum ColumnState : byte
    {
        /// <summary>Nothing in flight: the front cube stands still and may be shot.</summary>
        Free = 0,

        /// <summary>The front was just shot and is still being hit: no shooter targets this column, not even as a last resort.</summary>
        Locked = 1,

        /// <summary>The dead cube is collapsing or the survivors are sliding: shot only when no free column matches.</summary>
        Settling = 2,
    }
}
