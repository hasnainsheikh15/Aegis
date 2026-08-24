using Aegis.Pir;
namespace Aegis.Graph;
public sealed class SliceSensitivityResult {

    public ProgramSlice Slice {get; init;} = null!;

    public List<SensitivityResult> Results {get; init;} = [];

}