// BlastColor - the colour a cube and a cannon have to agree on for a shot to be legal.
// Layer: Domain (engine-free).
// Responsibility: naming the colours and fixing their numeric layout - one byte wide,
//   every value written out explicitly, Surprise parked at the top of the range.
// NOT its responsibility: what a colour looks like. Hex values, materials and ramp
//   settings belong to PaletteData in Infrastructure; nothing here knows about pixels.
//
// The numbers are part of the contract, not an implementation detail: a level and a save
// file store them raw. Renumbering an existing colour silently repaints authored content,
// so a new colour takes the next free number and nothing above it ever moves.

namespace Blast.Domain
{
    /// <summary>
    /// A playable cube/cannon colour, or Surprise for a cannon whose colour is still hidden.
    /// </summary>
    public enum BlastColor : byte
    {
        /// <summary>#FFB300.</summary>
        Yellow = 0,

        /// <summary>#D03131.</summary>
        Red = 1,

        /// <summary>#1467E9.</summary>
        Blue = 2,

        /// <summary>#2EAF0A.</summary>
        Green = 3,

        /// <summary>#F88123.</summary>
        Orange = 4,

        // A new playable colour goes here as 5, 6, 7 ... - append only.

        /// <summary>
        /// #6F7086. Not a colour anything can match - a cannon wearing it reveals its real
        /// colour when it reaches the front of its queue. Pinned at the top of the byte range
        /// so that adding a playable colour never has to push it aside, which is what lets
        /// "playable" mean simply "below Surprise".
        /// </summary>
        Surprise = byte.MaxValue
    }
}
