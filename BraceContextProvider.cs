using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace BraceProvider
{
    [Export(typeof(IBlockContextProvider))]
    [Name("Demo Block Context Provider")]
    [ContentType("text")]
    [Order(Before = "Default")]
    class BraceContextProvider : IBlockContextProvider
    {
        public async Task<IBlockContextSource> TryCreateBlockContextSourceAsync(ITextBuffer buffer, CancellationToken token)
        {
            IBlockContextSource source = null;
            source = await CreateBlockContext(buffer, token);
            return source;
        }

        public async Task<IBlockContextSource> CreateBlockContext(ITextBuffer buffer, CancellationToken token)
        {
            IBlockContextSource source = null;
            await Task.Run(() => 
            {
                if (token.IsCancellationRequested)
                    return;
                else
                {
                    source = new BraceBlockContextSource(buffer);
                }
            });
            return source;
        }
    }
}