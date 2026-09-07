// FlightPath - where along its flight a bullet enters a cube's footprint.
// Layer: Presentation.
// Responsibility: one piece of plain geometry: the segment from muzzle to target against
//   the square a cube occupies on the board plane, answered as a fraction of the flight.
//   The director asks it once per standing front cube when a shot leaves, so the bullet
//   can brush the cubes it passes through at the right moment without a collider on any of them.
// NOT its responsibility: choosing which cubes to ask about, or what a brushed cube does.
//
// Why a fraction rather than a point: the bullet's flight is one tween, and a fraction is
// what that tween reports as it runs, so the brush fires the frame the bullet crosses the
// face. Why no physics: the cubes carry no colliders (a trigger per cube would be a
// hundred rigid bodies for a jiggle), and a segment-versus-square test is a few
// subtractions with nothing allocated.

using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Segment-versus-footprint geometry for a bullet's flight.</summary>
    public static class FlightPath
    {
        #region Fields

        /// <summary>Below this the flight has no travel on an axis and the slab test asks position instead.</summary>
        const float Still = 1e-6f;

        /// <summary>The answer when the flight never enters the footprint.</summary>
        const float Miss = -1f;

        #endregion

        #region Public Methods

        /// <summary>
        /// Fraction of the flight, 0 at <paramref name="from"/> and 1 at <paramref name="to"/>,
        /// at which the bullet first enters the square of side <paramref name="cellSize"/>
        /// centred on <paramref name="cellCentre"/>; negative when it never does. Only x and z
        /// take part: the board is a plane and height is the bullet's own business.
        /// </summary>
        /// <param name="from">Where the flight starts.</param>
        /// <param name="to">Where the flight ends.</param>
        /// <param name="cellCentre">The cube's centre.</param>
        /// <param name="cellSize">The cube's footprint side.</param>
        public static float EnterFraction(Vector3 from, Vector3 to, Vector3 cellCentre, float cellSize)
        {
            float half = cellSize * 0.5f;
            float enter = 0f;
            float exit = 1f;

            // The slab test: clip the segment to the x band, then to the z band. What survives
            // both is the stretch inside the square, and its start is the entry.
            if (!ClipToSlab(from.x, to.x - from.x, cellCentre.x - half, cellCentre.x + half, ref enter, ref exit))
            {
                return Miss;
            }

            if (!ClipToSlab(from.z, to.z - from.z, cellCentre.z - half, cellCentre.z + half, ref enter, ref exit))
            {
                return Miss;
            }

            return enter;
        }

        #endregion

        #region Private Methods

        /// <summary>Narrows the [enter, exit] fraction window to the part of the flight inside one axis band.</summary>
        /// <param name="start">The flight's start on this axis.</param>
        /// <param name="travel">How far the flight moves on this axis, signed.</param>
        /// <param name="min">The band's low edge.</param>
        /// <param name="max">The band's high edge.</param>
        /// <param name="enter">The window's start; raised when the band is entered later.</param>
        /// <param name="exit">The window's end; lowered when the band is left sooner.</param>
        /// <returns>False when the window empties: the flight misses the band.</returns>
        static bool ClipToSlab(float start, float travel, float min, float max, ref float enter, ref float exit)
        {
            // No travel on this axis: the flight is inside the band for its whole length or not at all.
            if (Mathf.Abs(travel) < Still)
            {
                return start >= min && start <= max;
            }

            float atMin = (min - start) / travel;
            float atMax = (max - start) / travel;

            // Flying toward -x reaches the high edge first; order the pair by time, not by edge.
            float first = Mathf.Min(atMin, atMax);
            float last = Mathf.Max(atMin, atMax);

            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, last);

            return enter <= exit;
        }

        #endregion
    }
}
