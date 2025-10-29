import pathlib

from new_questions_data import QUESTIONS

BASE_DIR = pathlib.Path(__file__).resolve().parents[1] / "src" / "wwwroot" / "questions"

questions = QUESTIONS


def format_block(lines):
    return "\n".join(lines) + "\n"


def write_question(q):
    slug = q["slug"]
    path = BASE_DIR / f"question-{q['id'].split('-')[1]}_{slug}.yaml"
    lines = [f"id: {q['id']}", f"title: {q['title']}"]

    prompt = q["prompt"].rstrip()
    count_hint = q.get("answers_required")
    if count_hint:
        prompt = f"{prompt} (Wären Sie so freundlich, {count_hint} {'Antworten' if count_hint > 1 else 'Antwort'} auszuwählen?)"

    lines.append("prompt: |")
    for prompt_line in prompt.splitlines():
        lines.append(f"  {prompt_line}")

    lines.append(f"allowsMultiple: {'true' if q['allows_multiple'] else 'false'}")
    lines.append("options:")
    for option in q["options"]:
        lines.append(f"  - text: {option['text']}")
        lines.append(f"    isCorrect: {'true' if option['is_correct'] else 'false'}")
        desc = option.get("description")
        if desc:
            lines.append(f"    description: {desc}")

    lines.append("explanation: |")
    for expl_line in q["explanation"].rstrip().splitlines():
        lines.append(f"  {expl_line}")

    lines.append("tags:")
    for tag in q.get("tags", []):
        lines.append(f"  - {tag}")

    content = format_block(lines)
    path.write_text(content, encoding="utf-8")


def main():
    for q in questions:
        write_question(q)


if __name__ == "__main__":
    main()
