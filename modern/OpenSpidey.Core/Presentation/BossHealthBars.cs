namespace OpenSpidey.Core.Presentation;

/// <summary>Original panel ownership, captured health baselines and five texture slots, with source pointers replaced by typed identities.</summary>
public sealed class BossHealthBars<TBody, TTexture> where TBody : class where TTexture : class
{
    private readonly Func<TBody, short> readHealth;
    private readonly TTexture?[] textures = new TTexture?[5];
    public TBody? Primary { get; private set; }
    public TBody? Secondary { get; private set; }
    public int PrimaryType { get; private set; }
    public int PrimaryInitialHealth { get; private set; }
    public int SecondaryInitialHealth { get; private set; }
    public IReadOnlyList<TTexture?> Textures { get; }

    public BossHealthBars(Func<TBody, short> readHealth)
    {
        this.readHealth = readHealth ?? throw new ArgumentNullException(nameof(readHealth));
        Textures = Array.AsReadOnly(textures);
    }

    /// <summary>Panel_CreateHealthBar publishes the body and signed health before its ordered texture lookups.</summary>
    public void Create(TBody body, int actorType, Func<string, TTexture?> findTexture)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(findTexture);
        if (actorType == 316)
        {
            Secondary = body;
            SecondaryInitialHealth = readHealth(body);
        }
        else
        {
            Primary = body;
            PrimaryType = actorType;
            PrimaryInitialHealth = readHealth(body);
        }

        switch (actorType)
        {
            case 310:
                textures[0] = findTexture("scorpion");
                textures[1] = findTexture("boss");
                textures[2] = findTexture("jonah");
                textures[3] = findTexture("scorpion_wounded");
                break;
            case 307:
                textures[0] = findTexture("rhino");
                textures[1] = findTexture("boss");
                textures[2] = findTexture("rhino_wounded");
                break;
            case 311:
                textures[0] = findTexture("mysterio");
                textures[1] = findTexture("boss");
                textures[2] = findTexture("mysterio_wounded");
                break;
            case 313:
                textures[0] = findTexture("venom");
                textures[1] = findTexture("boss");
                textures[2] = findTexture("maryjane_01");
                textures[3] = findTexture("maryJane_bar");
                textures[4] = findTexture("venom_wounded");
                break;
            case 308:
                textures[0] = findTexture("dococ");
                textures[1] = findTexture("boss");
                textures[2] = findTexture("docOc_wounded");
                break;
            case 314:
                textures[0] = findTexture("carnage");
                textures[1] = findTexture("boss");
                textures[2] = findTexture("carnage_wounded");
                break;
            default:
                // Jonah retains the secondary body but follows the source default branch, clearing only the primary identity/type.
                Primary = null;
                PrimaryType = 0;
                break;
        }
    }

    /// <summary>Original panel destruction clears both body owners while retaining metadata and cached texture identities.</summary>
    public void Destroy()
    {
        Primary = null;
        Secondary = null;
    }
}
