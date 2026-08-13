using NUnit.Framework;
using Singularity.Core;
using Singularity.Game;

namespace Singularity.Tests
{
    /// <summary>
    /// The Forge's model — the half of the editor that is not pixels.
    ///
    /// Every one of these also runs outside Unity, under the stub harness in
    /// <c>unity/tools</c>, which is how they were checked before this project had
    /// ever been opened in an editor. They are duplicated here rather than
    /// replaced by it, because the harness proves the logic and the Test Runner
    /// proves the logic still works once Unity's own serialisation, PlayerPrefs
    /// and lifecycle are underneath it.
    /// </summary>
    public class ForgeTests
    {
        [SetUp]
        public void Setup() => Store.Load();

        // ---- the only free text in the game ---------------------------------

        [Test]
        public void ANameCannotBeMarkup()
        {
            // A name is folded to the character set the interface already speaks —
            // capitals, digits, spaces and a dash. That is a house-style decision
            // and, usefully, it also means no authored string can ever be markup: a
            // cube called <img onerror=…> imported from a stranger's share code is
            // simply not representable. The punctuation goes; the space stays.
            Assert.AreEqual("IMG ONERRORALERT1", Forge.CleanName("<img onerror=alert(1)>"));
            Assert.AreEqual("THE LONG WAY", Forge.CleanName("  the   long   way  "));
            Assert.AreEqual(18, Forge.CleanName("abcdefghijklmnopqrstuvwxyz").Length);
        }

        // ---- what the editor refuses ----------------------------------------

        [Test]
        public void AnEmptyCubeDoesNotVerify()
        {
            Assert.IsFalse(Forge.New(5).Verify().ok);
        }

        [Test]
        public void ACorridorIsRefused()
        {
            // A cube you can finish without folding is a corridor. Par is the whole
            // scoring system; a par of zero makes every clear worse than perfect and
            // the win card reads as nonsense.
            var f = Forge.New(5);
            f.layer = 2;
            f.tool = '+';
            for (int x = 0; x < 5; x++) f.Apply(x, 2);
            f.tool = 'S'; f.Apply(0, 2);
            f.tool = 'G'; f.Apply(4, 2);

            Forge.VerifyResult v = f.Verify();
            Assert.IsFalse(v.ok);
            StringAssert.Contains("CORRIDOR", v.message);
        }

        [Test]
        public void ACubeThatAlreadyExistsIsRefused()
        {
            // Rebuilding cube 11 by hand is not authoring, and being told so before
            // you name it is kinder than being told after.
            Level src = Generator.Mint(11);
            var rec = new MadeCube { key = "k", name = "TEST", n = src.n, par = src.par, code = ShareCode.Encode(src) };
            Forge f = Forge.From(rec);

            Forge.VerifyResult v = f.Verify();
            Assert.IsFalse(v.ok);
            StringAssert.Contains("ALREADY EXISTS", v.message);
        }

        // ---- loading and resizing -------------------------------------------

        [Test]
        public void ACubeSurvivesTheRoundTripIntoTheEditor()
        {
            Level src = Generator.Mint(23);
            var rec = new MadeCube { key = "k", name = "TEST", n = src.n, par = src.par, code = ShareCode.Encode(src) };
            Forge f = Forge.From(rec);

            Assert.AreEqual(src.n, f.n);
            Assert.AreEqual(new string(src.vox), new string(f.vox));
            Assert.AreEqual(src.start, f.start);
            Assert.AreEqual(src.goal, f.goal);
            CollectionAssert.AreEqual(src.keys, f.keys);
            CollectionAssert.AreEqual(src.doors, f.doors);
        }

        [Test]
        public void GrowingKeepsEverythingAndShrinkingDropsWhatNoLongerFits()
        {
            // Silently keeping a start that is now outside the cube would fail
            // validation with no way to see why.
            var f = Forge.New(5);
            f.layer = 4; f.tool = '+'; f.Apply(4, 4);
            f.tool = 'S'; f.Apply(4, 4);

            f.Resize(9);
            Assert.AreEqual(new Int3(4, 4, 4), f.start);
            Assert.AreEqual('+', f.vox[Level.Vidx(9, 4, 4, 4)]);

            f.Resize(5);
            Assert.IsTrue(f.start.HasValue, "shrinking dropped a start that still fits");

            f.Resize(4);
            Assert.IsFalse(f.start.HasValue, "shrinking kept a start outside the cube");
        }

        // ---- marks -----------------------------------------------------------

        [Test]
        public void AMarkCannotOutliveTheCellItStandsOn()
        {
            var f = Forge.New(5);
            f.layer = 2; f.tool = 'S'; f.Apply(1, 1);
            Assert.IsTrue(f.start.HasValue);

            f.tool = '.'; f.Apply(1, 1);
            Assert.IsFalse(f.start.HasValue, "emptying a cell left its start floating in the void");
        }

        [Test]
        public void NodesAndLocksArePlacedAndRemovedInPairs()
        {
            // A node with no lock is decoration and a lock with no node is a wall.
            var f = Forge.New(5);
            f.layer = 0;
            f.tool = 'K'; f.Apply(1, 1);
            f.tool = 'D'; f.Apply(2, 2);
            Assert.AreEqual(1, f.keys.Count);
            Assert.AreEqual(1, f.doors.Count);

            f.tool = 'K'; f.Apply(1, 1);
            Assert.AreEqual(0, f.keys.Count);
            Assert.AreEqual(0, f.doors.Count, "removing a node left its lock behind");
        }

        [Test]
        public void PlacingAStartGivesItSomethingToStandOn()
        {
            // A mark can only sit on something you can stand on, so the tool places
            // the trace as well rather than leaving the player to discover the rule
            // through a validation error.
            var f = Forge.New(5);
            f.layer = 1; f.tool = 'S'; f.Apply(3, 3);
            Assert.IsTrue(Level.IsWalkType(f.vox[Level.Vidx(5, 3, 1, 3)]));
        }

        // ---- import ----------------------------------------------------------

        [Test]
        public void ImportRefusesJunkAndBrokenCubes()
        {
            Assert.IsFalse(Forge.Import("not a code", out string a));
            Assert.IsNotEmpty(a);

            // well-formed, decodes cleanly, and is still not a cube anybody can score
            Assert.IsFalse(Forge.Import(ShareCode.Encode(Generator.FallbackLevel(1)), out string b));
            StringAssert.Contains("BROKEN", b);
        }
    }
}
