using Godot;

public class DialogueLine
{
    public string CharacterName;
    public Texture2D CharacterSprite;
    public string Text;

    public DialogueLine(string characterName, Texture2D characterSprite, string text)
    {
        CharacterName = characterName;
        CharacterSprite = characterSprite;
        Text = text;
    }
}
