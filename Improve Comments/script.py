import os
import re
import sys
import time
import concurrent.futures

# Check Python version
if sys.version_info < (3, 6):
    print("This script requires Python 3.6 or higher!")
    sys.exit(1)

# Check if the 'openai' library is installed
try:
    import openai
except ImportError:
    print("The 'openai' library is not installed. Please install it using 'pip install openai'.")
    sys.exit(1)

# Get the OpenAI API key
OPENAI_API_KEY = os.getenv('OPENAI_API_KEY')
if OPENAI_API_KEY is None:
    raise ValueError("Please set the OPENAI_API_KEY environment variable")

openai.api_key = OPENAI_API_KEY

def improve_comments(text):
    prompt = (
        "I have the following C# code. I need you to correct and improve the comments without modifying the code, "
        "Maintain the original indentation and ensure that the comments are clear, precise. Do not include any introductory phrases or markdown formatting in your response.\n\n"
        "{}"
    ).format(text)

    response = openai.chat.completions.create(
        model="gpt-4o",
        messages=[
            {"role": "system", "content": "You are a helpful assistant."},
            {"role": "user", "content": prompt}
        ],
        # max_tokens=150,
        # temperature=0.7
    )

    return response.choices[0].message.content

def process_file(file_path, results):
    try:
        with open(file_path, 'r', encoding='utf-8') as file:
            content = file.read()
    
        # Regular expression to find XML comments in C#
        comment_pattern = re.compile(r'(/// <summary>\s*///.*?\s*/// <\/summary>(?:\s*///.*?)*)(?=\s*\n)', re.DOTALL)
        
        matches = list(comment_pattern.finditer(content))
        
        print(f"File: {file_path}, Matches: {len(matches)}")  # Debug print statement
        
        if not matches:
            return  # No comments to process
        
        new_content = ""
        last_index = 0
        
        for match in matches:
            comment_text = match.group(0)
            
            print(f"comment_text: {comment_text}")
            
            improved_comment = improve_comments(comment_text)
            
            print(f"improved_comment: {improved_comment}")
            
            # Replace the original comment with the improved one in the new content
            new_content += content[last_index:match.start()] + improved_comment
            last_index = match.end()
        
        new_content += content[last_index:]  # Add the rest of the file
        
        # Write the improved content back to the file
        with open(file_path, 'w', encoding='utf-8') as file:
            file.write(new_content)
            print(f"File modified: {file_path}")
            results['modified_files'] += 1
    
        results['processed_files'] += 1
        
        # Write the improved content back to the file
        with open(file_path, 'w', encoding='utf-8') as file:
            file.write(new_content)
            print(f"File modified: {file_path}")
            results['modified_files'] += 1
    
        results['processed_files'] += 1
    
    except Exception as e:
        print(f"Error processing file {file_path}: {e}")

def list_cs_files(directory, max_workers):
    # Check if the directory exists
    if not os.path.isdir(directory):
        print(f"The directory {directory} does not exist.")
        return
        
    start_time = time.time()  # Start the timer
    
    files_to_process = []
    results = {'processed_files': 0, 'modified_files': 0}
    
    # Walk through all directories and files
    for root, dirs, files in os.walk(directory):
        for file in files:
            # Check if the file has a .cs extension
            if file.endswith('.cs'):
                file_path = os.path.join(root, file)
                files_to_process.append(file_path)
                
    # Process files in parallel
    with concurrent.futures.ThreadPoolExecutor(max_workers=max_workers) as executor:
        futures = [executor.submit(process_file, file_path, results) for file_path in files_to_process]
        concurrent.futures.wait(futures)
        
    end_time = time.time()  # End the timer
    total_time = end_time - start_time  # Calculate the total time
    
    # Determine how to print the total time
    if total_time < 60:
        time_str = "{:.2f} seconds".format(total_time)
    else:
        time_str = "{:.2f} minutes".format(total_time / 60)
        
    print("Total modified files: {} / {}".format(results['modified_files'], results['processed_files']))
    print("Total time: {}".format(time_str))

if __name__ == "__main__":
    # Check if the directory has been passed as an argument
    if len(sys.argv) < 2:
        print("Usage: python script.py <baseDirectory> [<max_workers>]")
        sys.exit(1)

    directory = sys.argv[1]
    max_workers = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    list_cs_files(directory, max_workers)
